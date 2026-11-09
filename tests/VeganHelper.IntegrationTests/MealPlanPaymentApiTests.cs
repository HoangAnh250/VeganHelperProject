using System.Net;
using System.Net.Http.Json;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using VeganHelper.BLL.DTOs;
using VeganHelper.BLL.DTOs.MealPlanning;
using VeganHelper.DAL.Entities;
using VeganHelper.DAL.Repositories;

namespace VeganHelper.IntegrationTests;

[Collection("Member2 API")]
public sealed class MealPlanPaymentApiTests(Member2ApiFixture fixture)
{
    [Fact]
    public async Task Packages_ArePublicAndUseTheAgreedPrices()
    {
        using var client = fixture.Factory.Client();

        var response = await client.GetAsync("/api/meal-plan-payments/packages");
        var packages = await response.Content.ReadFromJsonAsync<List<MealPlanPackageDto>>();
        var catalog = packages ?? throw new InvalidOperationException("Package API returned an empty body.");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(new[] { 1_000, 5_000, 10_000 }, catalog.Select(p => p.AmountVnd));
        Assert.Equal(new[] { 1, 6, 12 }, catalog.Select(p => p.DurationMonths));
    }

    [Fact]
    public async Task PaymentTablesAndOrderSequence_AreProtectedFromSupabaseBrowserRoles()
    {
        await using var connection=new NpgsqlConnection(fixture.Connection);
        await connection.OpenAsync();
        await using var tablesCommand=new NpgsqlCommand("""
            SELECT COUNT(*) FROM pg_class c JOIN pg_namespace n ON n.oid = c.relnamespace
            WHERE n.nspname = 'public' AND c.relname IN ('meal_plan_packages','meal_plan_payment_orders','user_meal_plan_access')
              AND c.relrowsecurity
              AND NOT EXISTS (SELECT 1 FROM aclexplode(COALESCE(c.relacl, acldefault('r', c.relowner))) a WHERE a.grantee = 0)
              AND NOT EXISTS (
                  SELECT 1 FROM pg_roles r WHERE r.rolname IN ('anon','authenticated')
                  AND has_table_privilege(r.oid, c.oid, 'SELECT,INSERT,UPDATE,DELETE'))
            """,connection);
        var protectedTableCount=(long)(await tablesCommand.ExecuteScalarAsync())!;

        await using var sequenceCommand=new NpgsqlCommand("""
            SELECT COUNT(*) FROM pg_class c JOIN pg_namespace n ON n.oid = c.relnamespace
            WHERE n.nspname = 'public' AND c.relname = 'meal_plan_payment_order_code_seq' AND c.relkind = 'S'
              AND NOT EXISTS (SELECT 1 FROM aclexplode(COALESCE(c.relacl, acldefault('S', c.relowner))) a WHERE a.grantee = 0)
              AND NOT EXISTS (
                  SELECT 1 FROM pg_roles r WHERE r.rolname IN ('anon','authenticated')
                  AND has_sequence_privilege(r.oid, c.oid, 'USAGE,SELECT,UPDATE'))
            """,connection);
        var protectedSequenceCount=(long)(await sequenceCommand.ExecuteScalarAsync())!;

        Assert.Equal(3,protectedTableCount);
        Assert.Equal(1,protectedSequenceCount);
    }

    [Fact]
    public async Task Checkout_WebhookIsIdempotentAndGrantsAccessOnlyAfterPayment()
    {
        var userId = await SeedUserAsync();
        using var client = fixture.Factory.Client(userId);

        var checkoutResponse = await client.PostAsJsonAsync("/api/meal-plan-payments/checkout", new { packageCode = "yearly" });
        var checkout = await checkoutResponse.Content.ReadFromJsonAsync<MealPlanCheckoutDto>();
        Assert.Equal(HttpStatusCode.Created, checkoutResponse.StatusCode);
        Assert.Equal(10_000, checkout!.AmountVnd);
        Assert.StartsWith("https://payments.example.invalid/", checkout.CheckoutUrl);

        var beforePayment = await client.GetFromJsonAsync<MealPlanAccessDto>("/api/meal-plan-payments/access");
        Assert.Equal("none", beforePayment!.Status);
        Assert.True(beforePayment.CanGenerate);
        Assert.Equal(3, beforePayment.TrialGenerationsRemaining);

        var webhook = new
        {
            signature = TestPayOSGateway.TestSignature,
            orderCode = checkout.OrderCode,
            amountVnd = checkout.AmountVnd,
            isSuccessful = true,
            currency = "VND",
            paymentLinkId = $"test-link-{checkout.OrderCode}",
            providerReference = "test-reference"
        };
        var first = await client.PostAsJsonAsync("/api/meal-plan-payments/webhook", webhook);
        var duplicate = await client.PostAsJsonAsync("/api/meal-plan-payments/webhook", webhook);
        var afterPayment = await client.GetFromJsonAsync<MealPlanAccessDto>("/api/meal-plan-payments/access");
        var order = await client.GetFromJsonAsync<MealPlanPaymentOrderDto>($"/api/meal-plan-payments/orders/{checkout.OrderId}");

        Assert.Equal(HttpStatusCode.OK, first.StatusCode);
        Assert.Equal(HttpStatusCode.OK, duplicate.StatusCode);
        Assert.Equal("active", afterPayment!.Status);
        Assert.True(afterPayment.CanGenerate);
        Assert.Equal(3, afterPayment.TrialGenerationsRemaining);
        Assert.Equal("paid", order!.Status);
        await using var db = fixture.Context();
        Assert.Equal(1, await db.UserMealPlanAccesses.CountAsync(a => a.UserId == userId));
    }

    [Fact]
    public async Task UnpaidUser_HasThreeTrialGenerationsAvailable()
    {
        var userId = await SeedUserAsync();
        using var client = fixture.Factory.Client(userId);

        var access = await client.GetFromJsonAsync<MealPlanAccessDto>("/api/meal-plan-payments/access");

        Assert.Equal("none", access!.Status);
        Assert.True(access.CanGenerate);
        Assert.Equal(3, access.TrialGenerationsRemaining);
    }

    [Fact]
    public async Task UnpaidNewUser_CanListEmptyMealPlansWhileTrialQuotaIsAvailable()
    {
        var userId = await SeedUserAsync();
        using var client = fixture.Factory.Client(userId);

        var response = await client.GetAsync("/api/MealPlans");
        var page = await response.Content.ReadFromJsonAsync<PagedResult<MealPlanDto>>();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.NotNull(page);
        Assert.Empty(page.Items);
        Assert.Equal(0, page.TotalItems);
        Assert.Equal(1, page.PageIndex);
        Assert.Equal(10, page.PageSize);
        Assert.Equal(0, page.TotalPages);
    }

    [Fact]
    public async Task ExpiredPackage_AllowsReadOnlyAccessForSevenDaysThenBlocksViewing()
    {
        var userId = await SeedUserAsync();
        var now = DateTime.UtcNow;
        await SeedAccessAsync(userId, now.AddMonths(-1), now.AddDays(-3));
        using var client = fixture.Factory.Client(userId);

        var duringGrace = await client.GetFromJsonAsync<MealPlanAccessDto>("/api/meal-plan-payments/access");
        var readablePlans = await client.GetAsync("/api/MealPlans");
        var generation = await client.PostAsJsonAsync("/api/MealPlans/generate", new
        {
            startDate = DateOnly.FromDateTime(now)
        });

        Assert.Equal("grace_period", duringGrace!.Status);
        Assert.False(duringGrace.CanGenerate);
        Assert.Equal(3, duringGrace.TrialGenerationsRemaining);
        Assert.True(duringGrace.CanViewExistingPlans);
        Assert.Equal(HttpStatusCode.OK, readablePlans.StatusCode);
        Assert.Equal(HttpStatusCode.PaymentRequired, generation.StatusCode);
        Assert.Contains("MEAL_PLAN_PACKAGE_EXPIRED", await generation.Content.ReadAsStringAsync());

        await using (var db = fixture.Context())
        {
            await db.UserMealPlanAccesses.Where(a => a.UserId == userId)
                .ExecuteUpdateAsync(setters => setters.SetProperty(a => a.ExpiresAt, now.AddDays(-8)));
        }

        var afterGrace = await client.GetFromJsonAsync<MealPlanAccessDto>("/api/meal-plan-payments/access");
        var blockedPlans = await client.GetAsync("/api/MealPlans");
        Assert.Equal("expired", afterGrace!.Status);
        Assert.False(afterGrace.CanViewExistingPlans);
        Assert.Equal(HttpStatusCode.PaymentRequired, blockedPlans.StatusCode);
    }

    [Fact]
    public async Task ExpiryReminder_SendsOnceWithinSevenDaysAndQueuesBrowserPush()
    {
        var user = await SeedUserAsync();
        var now = DateTime.UtcNow;
        var expiry = now.AddDays(7);
        await SeedAccessAsync(user, now.AddDays(-20), expiry);
        await using (var db = fixture.Context())
        {
            db.Add(new BrowserPushSubscription { UserId = user, Endpoint = "https://push.example.invalid/" + user,
                EndpointHash = Guid.NewGuid().ToString("N"), P256dh = "test-key", Auth = "test-auth", IsActive = true, UpdatedAt = now });
            await db.SaveChangesAsync();
        }
        await RunExpiryReminderAsync(now.AddSeconds(-1));
        await using (var before = fixture.Context())
            Assert.False(await before.Set<Notification>().AnyAsync(n => n.UserId == user));
        await Task.WhenAll(RunExpiryReminderAsync(now), RunExpiryReminderAsync(now));
        await RunExpiryReminderAsync(now.AddDays(2));
        await using var check = fixture.Context();
        var notice = await check.Set<Notification>().SingleAsync(n => n.UserId == user);
        Assert.Equal("meal_reminder", notice.Type);
        Assert.Equal("/weekly-menu", notice.TargetUrl);
        Assert.Contains("expires soon", notice.Title);
        Assert.Contains("renew", notice.Message);
        Assert.Contains("UTC", notice.Message);
        Assert.Equal(1, await check.Set<NotificationPushDelivery>().CountAsync(p => p.NotificationId == notice.Id));
        // A later renewal receives its own reminder, without duplicating the old one.
        await SeedAccessAsync(user, expiry, expiry.AddMonths(1));
        await RunExpiryReminderAsync(expiry.AddMonths(1).AddDays(-6));
        Assert.Equal(2, await check.Set<Notification>().CountAsync(n => n.UserId == user));
    }

    [Fact]
    public async Task ExpiryReminder_CatchesUpLateRunsAndSkipsExpiredFutureRenewedAndInactiveUsers()
    {
        var now = DateTime.UtcNow;
        var late = await SeedUserAsync();
        var expired = await SeedUserAsync();
        var future = await SeedUserAsync();
        var renewed = await SeedUserAsync();
        var inactive = await SeedUserAsync();
        await SeedAccessAsync(late, now.AddDays(-25), now.AddDays(2));
        await SeedAccessAsync(expired, now.AddDays(-30), now);
        await SeedAccessAsync(future, now.AddDays(1), now.AddDays(5));
        await SeedAccessAsync(renewed, now.AddDays(-25), now.AddDays(2));
        await SeedAccessAsync(renewed, now.AddDays(2), now.AddDays(32));
        await SeedAccessAsync(inactive, now.AddDays(-25), now.AddDays(2));
        await using (var db = fixture.Context())
            await db.Users.Where(u => u.Id == inactive).ExecuteUpdateAsync(s => s.SetProperty(u => u.IsActive, false));
        await RunExpiryReminderAsync(now);
        await using var check = fixture.Context();
        var users = new[] { late, expired, future, renewed, inactive };
        var notified = await check.Set<Notification>().Where(n => users.Contains(n.UserId)).Select(n => n.UserId).ToListAsync();
        Assert.Equal(new[] { late }, notified);
    }

    private async Task RunExpiryReminderAsync(DateTime now)
    {
        await using var db = fixture.Context();
        var service = new VeganHelper.BLL.Services.MealPlanExpiryReminder(new MealPlanPaymentRepository(db),
            new NotificationRepository(db), new ReminderClock(now));
        await service.RunAsync(default);
    }

    private sealed class ReminderClock(DateTime now) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => new(now);
    }

    private async Task<long> SeedUserAsync()
    {
        await using var db = fixture.Context();
        var suffix = Guid.NewGuid().ToString("N");
        var user = new User
        {
            Username = "payment_" + suffix,
            Email = suffix + "@example.invalid",
            RoleId = 1,
            CreatedAt = DateTime.UtcNow
        };
        db.Users.Add(user);
        await db.SaveChangesAsync();
        return user.Id;
    }

    private async Task SeedAccessAsync(long userId, DateTime startsAt, DateTime expiresAt)
    {
        await using var db = fixture.Context();
        var order = new MealPlanPaymentOrder
        {
            UserId = userId,
            MealPlanPackageId = 1,
            PackageCodeSnapshot = "monthly",
            PackageNameSnapshot = "1 month",
            DurationMonthsSnapshot = 1,
            AmountVnd = 1_000,
            Status = "paid",
            CreatedAt = startsAt,
            PaidAt = startsAt
        };
        db.MealPlanPaymentOrders.Add(order);
        await db.SaveChangesAsync();
        db.UserMealPlanAccesses.Add(new UserMealPlanAccess
        {
            UserId = userId,
            MealPlanPackageId = 1,
            PaymentOrderId = order.Id,
            StartsAt = startsAt,
            ExpiresAt = expiresAt,
            CreatedAt = startsAt
        });
        await db.SaveChangesAsync();
    }
}
