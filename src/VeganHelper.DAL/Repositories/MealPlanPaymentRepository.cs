using Microsoft.EntityFrameworkCore;
using VeganHelper.DAL.Entities;
using VeganHelper.DAL.Persistence;

namespace VeganHelper.DAL.Repositories;

public sealed class MealPlanPaymentRepository(AppDbContext db) : IMealPlanPaymentRepository
{
    public async Task<IReadOnlyList<MealPlanPackage>> GetActivePackagesAsync(CancellationToken ct) =>
        await db.MealPlanPackages.AsNoTracking().Where(p => p.IsActive)
            .OrderBy(p => p.DurationMonths).ToListAsync(ct);

    public Task<MealPlanPackage?> GetActivePackageAsync(string code, CancellationToken ct) =>
        db.MealPlanPackages.AsNoTracking().SingleOrDefaultAsync(p => p.Code == code && p.IsActive, ct);

    public async Task<MealPlanPaymentOrder> CreatePendingOrderAsync(
        long userId,
        MealPlanPackage package,
        DateTime now,
        CancellationToken ct)
    {
        var order = new MealPlanPaymentOrder
        {
            UserId = userId,
            MealPlanPackageId = package.Id,
            PackageCodeSnapshot = package.Code,
            PackageNameSnapshot = package.Name,
            DurationMonthsSnapshot = package.DurationMonths,
            AmountVnd = package.AmountVnd,
            Status = "pending",
            CreatedAt = now
        };
        db.MealPlanPaymentOrders.Add(order);
        await db.SaveChangesAsync(ct);
        return order;
    }

    public async Task SaveCheckoutLinkAsync(
        long orderId,
        string paymentLinkId,
        string checkoutUrl,
        DateTime now,
        CancellationToken ct)
    {
        var order = await db.MealPlanPaymentOrders.SingleOrDefaultAsync(o => o.Id == orderId, ct)
            ?? throw new InvalidOperationException("Newly created payment order was not found.");
        order.PaymentLinkId = paymentLinkId;
        order.CheckoutUrl = checkoutUrl;
        order.UpdatedAt = now;
        await db.SaveChangesAsync(ct);
    }

    public async Task MarkOrderFailedAsync(long orderId, DateTime now, CancellationToken ct)
    {
        var order = await db.MealPlanPaymentOrders.SingleOrDefaultAsync(o => o.Id == orderId, ct);
        if (order is null || order.Status == "paid") return;
        order.Status = "failed";
        order.UpdatedAt = now;
        await db.SaveChangesAsync(ct);
    }

    public Task<MealPlanPaymentOrder?> GetOrderForUserAsync(long orderId, long userId, CancellationToken ct) =>
        db.MealPlanPaymentOrders.AsNoTracking().SingleOrDefaultAsync(o => o.Id == orderId && o.UserId == userId, ct);

    public Task<UserMealPlanAccess?> GetLatestAccessAsync(long userId, CancellationToken ct) =>
        db.UserMealPlanAccesses.AsNoTracking().Where(a => a.UserId == userId)
            .OrderByDescending(a => a.ExpiresAt).ThenByDescending(a => a.Id).FirstOrDefaultAsync(ct);

    public Task<bool> HasActiveAccessAsync(long userId, DateTime now, CancellationToken ct) =>
        db.UserMealPlanAccesses.AsNoTracking().AnyAsync(a => a.UserId == userId && a.StartsAt <= now && a.ExpiresAt > now, ct);

    public async Task<PaymentWebhookApplyResult> ApplyWebhookAsync(
        long orderCode,
        int amountVnd,
        bool successful,
        string currency,
        string? paymentLinkId,
        string? providerReference,
        DateTime receivedAt,
        CancellationToken ct)
    {
        await using var transaction = await db.Database.BeginTransactionAsync(ct);
        var order = await db.MealPlanPaymentOrders.FromSqlInterpolated($"""
                SELECT * FROM public.meal_plan_payment_orders
                WHERE order_code = {orderCode}
                FOR UPDATE
                """)
            .SingleOrDefaultAsync(ct);

        if (order is null)
        {
            await transaction.CommitAsync(ct);
            return PaymentWebhookApplyResult.UnknownOrder;
        }

        if (order.AmountVnd != amountVnd || !string.Equals(currency, "VND", StringComparison.OrdinalIgnoreCase))
        {
            await transaction.CommitAsync(ct);
            return PaymentWebhookApplyResult.AmountMismatch;
        }

        if (!string.IsNullOrWhiteSpace(order.PaymentLinkId) &&
            !string.IsNullOrWhiteSpace(paymentLinkId) &&
            !string.Equals(order.PaymentLinkId, paymentLinkId, StringComparison.Ordinal))
        {
            await transaction.CommitAsync(ct);
            return PaymentWebhookApplyResult.PaymentLinkMismatch;
        }

        if (order.Status == "paid")
        {
            await transaction.CommitAsync(ct);
            return PaymentWebhookApplyResult.AlreadyApplied;
        }

        order.ProviderReference = Limit(providerReference, 100);
        order.UpdatedAt = receivedAt;

        if (!successful)
        {
            order.Status = "failed";
            await db.SaveChangesAsync(ct);
            await transaction.CommitAsync(ct);
            return PaymentWebhookApplyResult.Applied;
        }

        // Serialize renewals by locking the owning user row. This prevents two paid orders
        // arriving at once from calculating the same expiry and overwriting one another.
        await db.Database.ExecuteSqlInterpolatedAsync(
            $"SELECT id FROM public.users WHERE id = {order.UserId} FOR UPDATE", ct);

        var previousExpiry = await db.UserMealPlanAccesses.AsNoTracking()
            .Where(a => a.UserId == order.UserId)
            .MaxAsync(a => (DateTime?)a.ExpiresAt, ct);
        var startsAt = previousExpiry is { } expiry && expiry > receivedAt ? expiry : receivedAt;
        var expiresAt = startsAt.AddMonths(order.DurationMonthsSnapshot);

        order.Status = "paid";
        order.PaidAt = receivedAt;
        order.PaymentLinkId ??= Limit(paymentLinkId, 100);
        db.UserMealPlanAccesses.Add(new UserMealPlanAccess
        {
            UserId = order.UserId,
            MealPlanPackageId = order.MealPlanPackageId,
            PaymentOrderId = order.Id,
            StartsAt = startsAt,
            ExpiresAt = expiresAt,
            CreatedAt = receivedAt
        });

        await db.SaveChangesAsync(ct);
        await transaction.CommitAsync(ct);
        return PaymentWebhookApplyResult.Applied;
    }

    private static string? Limit(string? value, int maxLength) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Length <= maxLength ? value : value[..maxLength];
}
