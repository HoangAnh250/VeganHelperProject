using System.Net;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using VeganHelper.DAL.Entities;
using VeganHelper.DAL.Repositories;

namespace VeganHelper.IntegrationTests;

[Collection("Member2 API")]
public sealed class NotificationApiTests(Member2ApiFixture fixture)
{
    private async Task<long> UserAsync()
    {
        await using var db = fixture.Context();
        var suffix = Guid.NewGuid().ToString("N");
        var user = new User { Username = "notify_" + suffix, Email = suffix + "@example.invalid", RoleId = 1, CreatedAt = DateTime.UtcNow };
        db.Users.Add(user); await db.SaveChangesAsync(); return user.Id;
    }
    private static Notification Message(long user, string key) => new()
    {
        UserId = user,
        EventKey = key,
        Type = "comment",
        Title = "Comment",
        Message = "New comment",
        TargetUrl = "/posts/1",
        CreatedAt = DateTime.UtcNow
    };
    private async Task<long> PublishAsync(long user, string key)
    {
        await using var db = fixture.Context();
        return await new NotificationRepository(db).PublishAsync(Message(user, key), default);
    }
    private static BrowserPushSubscription Subscription(long user, string suffix)
    {
        using var key = ECDsa.Create(ECCurve.NamedCurves.nistP256);
        var point = key.ExportParameters(false).Q;
        var bytes = new byte[65]; bytes[0] = 4; point.X!.CopyTo(bytes, 1); point.Y!.CopyTo(bytes, 33);
        static string Encode(byte[] value) => Convert.ToBase64String(value).TrimEnd('=').Replace('+', '-').Replace('/', '_');
        var endpoint = "https://fcm.googleapis.com/fcm/send/" + suffix;
        return new()
        {
            UserId = user,
            Endpoint = endpoint,
            EndpointHash = Convert.ToHexString(SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(endpoint))),
            P256dh = Encode(bytes),
            Auth = Encode(RandomNumberGenerator.GetBytes(16)),
            IsActive = true,
            UpdatedAt = DateTime.UtcNow
        };
    }
    [Theory]
    [InlineData("GET", "/api/notifications")]
    [InlineData("GET", "/api/notifications/unread-count")]
    [InlineData("GET", "/api/notifications/push/config")]
    [InlineData("PATCH", "/api/notifications/1/read")]
    [InlineData("PATCH", "/api/notifications/read-all")]
    [InlineData("DELETE", "/api/notifications/push/subscriptions/1")]
    public async Task Anonymous_IsUnauthorized(string method, string path)
    {
        using var client = fixture.Factory.Client();
        var response = await client.SendAsync(new HttpRequestMessage(new HttpMethod(method), path));
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }
    [Fact]
    public async Task ListAndRead_EnforceOwnershipAndIdempotency()
    {
        var owner = await UserAsync(); var other = await UserAsync();
        var first = await PublishAsync(owner, "one"); await PublishAsync(owner, "two"); var foreign = await PublishAsync(other, "foreign");
        using var client = fixture.Factory.Client(owner);
        var list = await client.GetFromJsonAsync<JsonElement>("/api/notifications?PageSize=1");
        Assert.Equal(2, list.GetProperty("totalCount").GetInt32());
        Assert.Equal(1, list.GetProperty("items").GetArrayLength());
        Assert.Equal(HttpStatusCode.NotFound, (await client.PatchAsync($"/api/notifications/{foreign}/read", null)).StatusCode);
        var read = await client.PatchAsync($"/api/notifications/{first}/read", null);
        Assert.Equal(1, (await read.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("updatedCount").GetInt32());
        DateTime? readAt;
        await using (var before = fixture.Context()) readAt = (await before.Set<Notification>().SingleAsync(n => n.Id == first)).ReadAt;
        var repeated = await client.PatchAsync($"/api/notifications/{first}/read", null);
        Assert.Equal(0, (await repeated.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("updatedCount").GetInt32());
        var all = await client.PatchAsync("/api/notifications/read-all", null);
        Assert.Equal(0, (await all.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("unreadCount").GetInt32());
        await using var db = fixture.Context();
        Assert.Equal(readAt, (await db.Set<Notification>().SingleAsync(n => n.Id == first)).ReadAt);
        Assert.False((await db.Set<Notification>().SingleAsync(n => n.Id == foreign)).IsRead);
        var newer = await PublishAsync(owner, "after-read-all");
        Assert.Equal(1, (await client.GetFromJsonAsync<JsonElement>("/api/notifications/unread-count")).GetProperty("unreadCount").GetInt32());
    }
    [Fact]
    public async Task Publication_IsIdempotentAndOutboxIsAtomic()
    {
        var user = await UserAsync();
        await using (var db = fixture.Context()) await new NotificationRepository(db).SubscribeAsync(Subscription(user, Guid.NewGuid().ToString("N")), default);
        var tasks = Enumerable.Range(0, 6).Select(_ => PublishAsync(user, "same-event"));
        var ids = await Task.WhenAll(tasks);
        Assert.Single(ids.Distinct());
        await using var verify = fixture.Context();
        Assert.Equal(1, await verify.Set<Notification>().CountAsync(n => n.UserId == user));
        Assert.Equal(1, await verify.Set<NotificationPushDelivery>().CountAsync(d => d.NotificationId == ids[0]));
    }
    [Fact]
    public async Task Subscription_IsIdempotentAndCannotBeTakenByOtherUser()
    {
        var user = await UserAsync(); var other = await UserAsync();
        var s = Subscription(user, Guid.NewGuid().ToString("N"));
        long id;
        await using (var db = fixture.Context()) id = await new NotificationRepository(db).SubscribeAsync(s, default);
        await using (var db = fixture.Context()) Assert.Equal(id, await new NotificationRepository(db).SubscribeAsync(s, default));
        s.UserId = other;
        await using (var db = fixture.Context()) Assert.Equal(0, await new NotificationRepository(db).SubscribeAsync(s, default));
        using var client = fixture.Factory.Client(other);
        Assert.Equal(HttpStatusCode.NotFound, (await client.DeleteAsync($"/api/notifications/push/subscriptions/{id}")).StatusCode);
        using var owner = fixture.Factory.Client(user);
        Assert.Equal(HttpStatusCode.NoContent, (await owner.DeleteAsync($"/api/notifications/push/subscriptions/{id}")).StatusCode);
        Assert.Equal(HttpStatusCode.NoContent, (await owner.DeleteAsync($"/api/notifications/push/subscriptions/{id}")).StatusCode);
        // Logging out releases an inactive endpoint for the next account on this browser.
        s.UserId = other; s.UpdatedAt = DateTime.UtcNow;
        await using (var db = fixture.Context()) Assert.Equal(id, await new NotificationRepository(db).SubscribeAsync(s, default));
        Assert.Equal(HttpStatusCode.NotFound, (await owner.DeleteAsync($"/api/notifications/push/subscriptions/{id}")).StatusCode);
        Assert.Equal(HttpStatusCode.NoContent, (await client.DeleteAsync($"/api/notifications/push/subscriptions/{id}")).StatusCode);
    }
    [Fact]
    public async Task DisabledPush_ReportsStatusAndRefusesSubscriptions()
    {
        using var client = fixture.Factory.Client(await UserAsync());
        var config = await client.GetFromJsonAsync<JsonElement>("/api/notifications/push/config");
        Assert.False(config.GetProperty("enabled").GetBoolean());
        Assert.False(config.TryGetProperty("privateKey", out _));
        Assert.Equal(HttpStatusCode.Conflict, (await client.PostAsJsonAsync("/api/notifications/push/subscriptions",
         new { endpoint = "https://fcm.googleapis.com/test", keys = new { p256dh = "test", auth = "test" } })).StatusCode);
    }
    [Fact]
    public async Task PendingAndProcessingLeases_ClaimOnlyOnceAndRecoverAfterExpiry()
    {
        var user = await UserAsync();
        await using (var db = fixture.Context()) await new NotificationRepository(db).SubscribeAsync(Subscription(user, Guid.NewGuid().ToString("N")), default);
        var id = await PublishAsync(user, "lease");
        var now = DateTime.UtcNow;
        Guid token = Guid.NewGuid();
        NotificationPushDelivery old;
        await using (var db = fixture.Context()) old = Assert.Single(await new NotificationRepository(db).ClaimPushAsync(now, token, default), d => d.NotificationId == id);
        await using (var db = fixture.Context()) Assert.DoesNotContain(await new NotificationRepository(db).ClaimPushAsync(now, Guid.NewGuid(), default), d => d.NotificationId == id);
        await using (var db = fixture.Context())
        {
            var repo = new NotificationRepository(db);
            var recovered = Assert.Single(await repo.ClaimPushAsync(now.AddMinutes(11), Guid.NewGuid(), default), d => d.NotificationId == id);
            Assert.Equal(2, recovered.Attempts);
            await repo.FinishPushAsync(old, "sent", null, now, default); // stale worker cannot acknowledge recovered work
        }
        await using var verify = fixture.Context();
        Assert.Equal("processing", (await verify.Set<NotificationPushDelivery>().SingleAsync(d => d.NotificationId == id)).State);
    }
    [Fact]
    public async Task FinalAttemptCrash_BecomesFailedAfterLeaseExpiry()
    {
        var user = await UserAsync();
        await using (var db = fixture.Context()) await new NotificationRepository(db).SubscribeAsync(Subscription(user, Guid.NewGuid().ToString("N")), default);
        var id = await PublishAsync(user, "final-attempt-crash");
        var now = DateTime.UtcNow;
        await using (var db = fixture.Context())
        {
            await db.Set<NotificationPushDelivery>().Where(d => d.NotificationId == id).ExecuteUpdateAsync(u => u
             .SetProperty(d => d.State, "processing").SetProperty(d => d.Attempts, 5)
             .SetProperty(d => d.LeaseToken, Guid.NewGuid()).SetProperty(d => d.LeaseUntil, now.AddMinutes(-1)));
        }
        await using (var db = fixture.Context()) Assert.DoesNotContain(await new NotificationRepository(db).ClaimPushAsync(now, Guid.NewGuid(), default), d => d.NotificationId == id);
        await using var verify = fixture.Context();
        var failed = await verify.Set<NotificationPushDelivery>().SingleAsync(d => d.NotificationId == id);
        Assert.Equal("failed", failed.State); Assert.Equal("lease_exhausted", failed.LastErrorCode);
    }
    [Fact]
    public async Task ReadInvalidPagination_ReturnsBadRequest()
    {
        using var client = fixture.Factory.Client(await UserAsync());
        Assert.Equal(HttpStatusCode.BadRequest, (await client.GetAsync("/api/notifications?PageSize=101")).StatusCode);
    }
}
