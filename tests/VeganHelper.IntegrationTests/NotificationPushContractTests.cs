using System.Net;
using System.Net.Http.Json;
using System.Security.Cryptography;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Npgsql;
using VeganHelper.API.Infrastructure.Notifications;
using VeganHelper.BLL.Contracts;
using VeganHelper.DAL.Entities;
using VeganHelper.DAL.Integrations;
using VeganHelper.DAL.Repositories;
using Microsoft.EntityFrameworkCore;
using Hangfire;
using Hangfire.PostgreSql;
using VeganHelper.API.BackgroundJobs;

namespace VeganHelper.IntegrationTests;

[Collection("Member2 API")]
public sealed class NotificationPushContractTests(Member2ApiFixture fixture)
{
    private static string Encode(byte[] bytes) => Convert.ToBase64String(bytes).TrimEnd('=').Replace('+', '-').Replace('/', '_');
    private static WebPushSettings Settings()
    {
        using var key = ECDsa.Create(ECCurve.NamedCurves.nistP256);
        var parts = key.ExportParameters(true); var point = new byte[65]; point[0] = 4;
        parts.Q.X!.CopyTo(point, 1); parts.Q.Y!.CopyTo(point, 33);
        return new() { Enabled = true, Subject = "mailto:notifications@example.invalid", PublicKey = Encode(point), PrivateKey = Encode(parts.D!) };
    }
    [Fact]
    public void EnabledPush_RequiresValidMatchingVapidKeys()
    {
        var validator = new WebPushOptionsValidator();
        Assert.True(validator.Validate(null, Settings()).Succeeded);
        Assert.True(validator.Validate(null, new WebPushSettings()).Succeeded);
        Assert.True(validator.Validate(null, new WebPushSettings { Enabled = true }).Failed);
        var mismatched = Settings(); mismatched.PrivateKey = Settings().PrivateKey;
        Assert.True(validator.Validate(null, mismatched).Failed);
    }
    private async Task<long> UserAsync()
    {
        await using var db = fixture.Context();
        var suffix = Guid.NewGuid().ToString("N");
        var user = new User { Username = "notify_" + suffix, Email = suffix + "@example.invalid", RoleId = 1, CreatedAt = DateTime.UtcNow };
        db.Users.Add(user); await db.SaveChangesAsync(); return user.Id;
    }
    [Fact]
    public async Task EnabledSubscriptionApi_ValidatesKeysAndSupportsRepeatedRegistration()
    {
        var vapid = Settings();
        using var factory = fixture.Factory.WithWebHostBuilder(builder => builder.ConfigureServices(services =>
         services.PostConfigure<WebPushSettings>(o => { o.Enabled = true; o.Subject = vapid.Subject; o.PublicKey = vapid.PublicKey; o.PrivateKey = vapid.PrivateKey; })));
        var user = await UserAsync();
        // Only the options are overridden: this test does not start Hangfire or send external push.
        using var client = fixture.Factory.Client(user);
        using var api = factory.CreateClient();
        api.DefaultRequestHeaders.Authorization = client.DefaultRequestHeaders.Authorization;
        var keys = Settings();
        var body = new
        {
            endpoint = "https://fcm.googleapis.com/fcm/send/" + Guid.NewGuid().ToString("N"),
            keys = new { p256dh = keys.PublicKey, auth = Encode(RandomNumberGenerator.GetBytes(16)) }
        };
        var response = await api.PostAsJsonAsync("/api/notifications/push/subscriptions", body);
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var first = await response.Content.ReadFromJsonAsync<SubscriptionId>();
        var repeated = await api.PostAsJsonAsync("/api/notifications/push/subscriptions", body);
        Assert.Equal(first!.Id, (await repeated.Content.ReadFromJsonAsync<SubscriptionId>())!.Id);
        var bad = await api.PostAsJsonAsync("/api/notifications/push/subscriptions", new
        { endpoint = "https://127.0.0.1/private", keys = body.keys });
        Assert.Equal(HttpStatusCode.BadRequest, bad.StatusCode);
    }
    private sealed record SubscriptionId(long Id);
    [Fact]
    public async Task WebPushTransport_EncryptsPayloadAndUsesVapidWithoutNetwork()
    {
        var settings = Settings();
        var handler = new CaptureHandler();
        using var http = new HttpClient(handler);
        var sub = new BrowserPushSubscription
        {
            Endpoint = "https://fcm.googleapis.com/test",
            P256dh = Settings().PublicKey,
            Auth = Encode(RandomNumberGenerator.GetBytes(16))
        };
        var result = await new WebPushTransport(http).SendAsync(sub,
         new Notification { Id = 9, Title = "Private notification title", Message = "Private notification body", TargetUrl = "/posts/1" }, settings, default);
        Assert.Equal(201, result);
        Assert.Equal("aes128gcm", handler.Encoding);
        Assert.True(handler.HasAuthorization);
        Assert.DoesNotContain("Private notification", System.Text.Encoding.UTF8.GetString(handler.Body));
        handler.Status = HttpStatusCode.Gone;
        Assert.Equal(410, await new WebPushTransport(http).SendAsync(sub, new Notification(), settings, default));
    }
    private sealed class CaptureHandler : HttpMessageHandler
    {
        public string? Encoding; public bool HasAuthorization; public byte[] Body = [];
        public HttpStatusCode Status = HttpStatusCode.Created;
        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
        {
            Encoding = request.Content!.Headers.ContentEncoding.Single(); HasAuthorization = request.Headers.Authorization is not null;
            Body = await request.Content.ReadAsByteArrayAsync(ct);
            return new HttpResponseMessage(Status);
        }
    }
    [Fact]
    public void HangfireStorage_PersistsDispatchJobInLocalPostgres()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddHangfire(config => config.UseSimpleAssemblyNameTypeSerializer().UseRecommendedSerializerSettings()
         .UsePostgreSqlStorage(storage => storage.UseNpgsqlConnection(fixture.Connection), new PostgreSqlStorageOptions { PrepareSchemaIfNecessary = true }));
        using var provider = services.BuildServiceProvider();
        var storage = provider.GetRequiredService<JobStorage>();
        var client = new BackgroundJobClient(storage);
        var id = client.Enqueue<NotificationPushJob>(job => job.RunAsync(CancellationToken.None));
        try
        {
            using var connection = storage.GetConnection();
            var saved = connection.GetJobData(id);
            Assert.Equal(typeof(NotificationPushJob), saved.Job.Type);
            Assert.Equal(nameof(NotificationPushJob.RunAsync), saved.Job.Method.Name);
            Assert.Equal("Enqueued", saved.State);
        }
        finally { client.Delete(id); }
    }
    [Fact]
    public async Task NotificationMigration_ProtectsAllThreeTablesFromDataApi()
    {
        await using var connection = new NpgsqlConnection(fixture.Connection); await connection.OpenAsync();
        await using var command = new NpgsqlCommand("""
   SELECT COUNT(*) FROM pg_class c JOIN pg_namespace n ON n.oid = c.relnamespace
   WHERE n.nspname = 'public' AND c.relname IN ('notifications','browser_push_subscriptions','notification_push_deliveries')
    AND c.relrowsecurity
    AND NOT EXISTS (SELECT 1 FROM aclexplode(COALESCE(c.relacl, acldefault('r', c.relowner))) a WHERE a.grantee = 0)
    AND NOT EXISTS (SELECT 1 FROM pg_roles r WHERE r.rolname IN ('anon','authenticated')
     AND has_table_privilege(r.oid, c.oid, 'SELECT,INSERT,UPDATE,DELETE'))
   """, connection);
        Assert.Equal(3L, await command.ExecuteScalarAsync());
    }
    [Fact]
    public async Task ExpiredDelivery_DisablesSubscriptionAndCannotReviveClaim()
    {
        var owner = await UserAsync();
        long id;
        await using (var db = fixture.Context())
        {
            var sub = new BrowserPushSubscription
            {
                UserId = owner,
                Endpoint = "https://fcm.googleapis.com/" + Guid.NewGuid(),
                EndpointHash = Guid.NewGuid().ToString("N"),
                P256dh = Settings().PublicKey,
                Auth = Encode(RandomNumberGenerator.GetBytes(16)),
                IsActive = true,
                UpdatedAt = DateTime.UtcNow
            };
            var repo = new NotificationRepository(db);
            await repo.SubscribeAsync(sub, default);
            id = await repo.PublishAsync(new Notification
            {
                UserId = owner,
                EventKey = "expiry",
                Type = "comment",
                Title = "test",
                Message = "test",
                TargetUrl = "/posts/1",
                CreatedAt = DateTime.UtcNow
            }, default);
        }
        await using (var db = fixture.Context())
        {
            var repo = new NotificationRepository(db);
            var row = Assert.Single(await repo.ClaimPushAsync(DateTime.UtcNow.AddDays(1), Guid.NewGuid(), default), d => d.NotificationId == id);
            await repo.FinishPushAsync(row, "expired", "http_410", DateTime.UtcNow, default);
        }
        await using var verify = fixture.Context();
        var delivery = await verify.Set<NotificationPushDelivery>().Include(d => d.Subscription).SingleAsync(d => d.NotificationId == id);
        Assert.Equal("expired", delivery.State); Assert.False(delivery.Subscription.IsActive);
    }
    [Fact]
    public async Task TypedPublishers_UseCommittedCommentAndReviewData()
    {
        var owner = await UserAsync(); var commenter = await UserAsync();
        long postId, commentId;
        await using (var db = fixture.Context())
        {
            var post = new Post { AuthorId = owner, Title = "Recipe", PostType = "recipe", Status = "published", CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow };
            db.Posts.Add(post); await db.SaveChangesAsync(); postId = post.Id;
            var comment = new Comment { PostId = post.Id, UserId = commenter, Content = "Comment", Status = "visible", CreatedAt = DateTime.UtcNow };
            db.Comments.Add(comment); await db.SaveChangesAsync(); commentId = comment.Id;
        }
        using var scope = fixture.Factory.Services.CreateScope();
        var publisher = scope.ServiceProvider.GetRequiredService<INotificationPublisher>();
        var first = await publisher.NotifyCommentAsync(commentId);
        Assert.NotNull(first);
        Assert.Equal(first, await publisher.NotifyCommentAsync(commentId));
        Assert.NotNull(await publisher.NotifyPostReviewAsync(postId));
        await using var verify = fixture.Context();
        Assert.Equal(2, await verify.Set<Notification>().CountAsync(n => n.UserId == owner));
    }
}
