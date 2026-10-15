using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using AutoMapper;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Npgsql;
using Microsoft.Extensions.Options;
using VeganHelper.DAL.Integrations;
using VeganHelper.BLL.Contracts;
using VeganHelper.BLL.DTOs.Comments;
using VeganHelper.BLL.Mapping;
using VeganHelper.BLL.Services;
using VeganHelper.DAL.Entities;
using VeganHelper.DAL.Persistence;
using VeganHelper.DAL.Repositories;

namespace VeganHelper.IntegrationTests;

[Collection("Member2 API")]
public sealed class SocialNotificationTests(Member2ApiFixture fixture)
{
    private static NotificationService Notifications(AppDbContext db) => new(new NotificationRepository(db),
        new MapperConfiguration(c => c.AddProfile<MappingProfile>(), NullLoggerFactory.Instance).CreateMapper(), TimeProvider.System);
    private static FollowService Follows(AppDbContext db) => new(new FollowRepository(db), Notifications(db));
    private static PostInteractionService Likes(AppDbContext db) => new(new PostInteractionRepository(db), Notifications(db));
    private static CommentService Comments(AppDbContext db) => new(new CommentRepository(db),
        new CreateCommentRequestValidator(), new UpdateCommentRequestValidator(), new ReportCommentRequestValidator(), new CommentKeywordFilter(), Notifications(db));
    private async Task<long> UserAsync()
    {
        await using var db = fixture.Context();
        var suffix = Guid.NewGuid().ToString("N");
        var user = new User { Username = "social_" + suffix, Email = suffix + "@example.invalid", RoleId = 1,
            IsActive = true, EmailVerifiedAt = DateTime.UtcNow, CreatedAt = DateTime.UtcNow };
        db.Users.Add(user); await db.SaveChangesAsync(); return user.Id;
    }
    private async Task FollowAsync(long actor, long author, bool enabled)
    {
        await using var db = fixture.Context();
        await Follows(db).FollowAsync(actor, author);
        if (enabled) await Follows(db).SetNewPostNotificationsAsync(actor, author, true);
    }
    private async Task<long> PostAsync(long author)
    {
        await using var db = fixture.Context();
        var repo = new PostRepository(db);
        await repo.BeginTransactionAsync();
        var post = await repo.CreatePostAsync(new Post { AuthorId = author, Title = "Plant-based ideas", Status = "published", CreatedAt = DateTime.UtcNow });
        await repo.CommitTransactionAsync(); return post.Id;
    }
    private async Task DispatchAsync()
    {
        await using var db = fixture.Context();
        await new FollowerPostNotificationDispatcher(new NotificationRepository(db)).DispatchBatchAsync();
    }

    [Fact]
    public async Task MyPosts_ReturnsActualLikeCountsIncludingZero_WithoutOtherAuthorsPosts()
    {
        var author = await UserAsync(); var actor = await UserAsync(); var another = await UserAsync();
        var liked = await PostAsync(author); var unliked = await PostAsync(author); var foreign = await PostAsync(actor);
        await using (var db = fixture.Context())
        {
            db.PostLikes.AddRange(new PostLike { UserId = actor, PostId = liked, CreatedAt = DateTime.UtcNow },
                new PostLike { UserId = another, PostId = liked, CreatedAt = DateTime.UtcNow },
                new PostLike { UserId = author, PostId = foreign, CreatedAt = DateTime.UtcNow });
            await db.SaveChangesAsync();
        }
        using var client = fixture.Factory.Client(author);
        var response = await client.GetFromJsonAsync<JsonElement>("/api/Posts/my-posts?pageIndex=1&pageSize=10");
        var items = response.GetProperty("items").EnumerateArray().ToList();
        Assert.Equal(2, items.Count);
        Assert.Equal(2, items.Single(p => p.GetProperty("id").GetInt64() == liked).GetProperty("likeCount").GetInt64());
        Assert.Equal(0, items.Single(p => p.GetProperty("id").GetInt64() == unliked).GetProperty("likeCount").GetInt64());
        Assert.DoesNotContain(items, p => p.GetProperty("id").GetInt64() == foreign);
    }

    [Fact]
    public async Task LikeUnlikeRelike_NotifiesOnceAndNeverNotifiesSelf()
    {
        var author = await UserAsync(); var actor = await UserAsync(); var post = await PostAsync(author);
        for (var i = 0; i < 3; i++)
        {
            await using var db = fixture.Context();
            await Likes(db).ToggleLikeAsync(actor, post);
        }
        await using (var db = fixture.Context()) await Likes(db).ToggleLikeAsync(author, post);
        await using var verify = fixture.Context();
        var n = Assert.Single(await verify.Set<Notification>().Where(n => n.EventKey == $"like:{post}:{actor}").ToListAsync());
        Assert.Equal(author, n.UserId); Assert.Equal("post_like", n.Type);
        Assert.False(await verify.Set<Notification>().AnyAsync(n => n.EventKey == $"like:{post}:{author}"));
    }

    [Fact]
    public async Task ConcurrentLikeToggles_SerializeAndKeepOneNotification()
    {
        var author = await UserAsync(); var actor = await UserAsync(); var post = await PostAsync(author);
        async Task Toggle() { await using var db = fixture.Context(); await Likes(db).ToggleLikeAsync(actor, post); }
        await Task.WhenAll(Toggle(), Toggle());
        await using var verify = fixture.Context();
        Assert.False(await verify.PostLikes.AnyAsync(l => l.PostId == post && l.UserId == actor));
        Assert.Equal(1, await verify.Set<Notification>().CountAsync(n => n.EventKey == $"like:{post}:{actor}"));
    }

    [Fact]
    public async Task RootAndReply_ChooseDifferentRecipientsAndHidePendingComments()
    {
        var author = await UserAsync(); var actor = await UserAsync(); var post = await PostAsync(author);
        long rootId;
        await using (var db = fixture.Context()) rootId = (await Comments(db).CreateCommentAsync(post, actor, new CreateCommentRequest { Content = "Looks delicious" })).Id;
        long replyId;
        await using (var db = fixture.Context()) replyId = (await Comments(db).CreateCommentAsync(post, author, new CreateCommentRequest { ParentCommentId = rootId, Content = "Thank you" })).Id;
        long hiddenId;
        await using (var db = fixture.Context()) hiddenId = (await Comments(db).CreateCommentAsync(post, actor, new CreateCommentRequest { Content = "Địt mẹ mày" })).Id;
        await using var verify = fixture.Context();
        Assert.Equal(author, (await verify.Set<Notification>().SingleAsync(n => n.EventKey == $"comment:{rootId}")).UserId);
        var reply = await verify.Set<Notification>().SingleAsync(n => n.EventKey == $"reply:{replyId}");
        Assert.Equal(actor, reply.UserId); Assert.Equal("comment_reply", reply.Type);
        Assert.False(await verify.Set<Notification>().AnyAsync(n => n.EventKey == $"comment:{replyId}" || n.EventKey == $"comment:{hiddenId}"));
    }

    [Fact]
    public async Task FollowAndPreference_HttpRequiresOwnershipAndExistingRelationship()
    {
        var author = await UserAsync(); var actor = await UserAsync(); var other = await UserAsync();
        using var client = fixture.Factory.Client(actor);
        var response = await client.PostAsync($"/api/users/{author}/follow", null);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var status = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.False(status.GetProperty("notifyOnNewPost").GetBoolean());
        Assert.Equal(HttpStatusCode.Conflict, (await client.PostAsync($"/api/users/{author}/follow", null)).StatusCode);
        using var outsider = fixture.Factory.Client(other);
        Assert.Equal(HttpStatusCode.NotFound, (await outsider.PatchAsJsonAsync($"/api/users/{author}/follow/notifications", new { notifyOnNewPost = true })).StatusCode);
        using var guest = fixture.Factory.Client();
        Assert.Equal(HttpStatusCode.Unauthorized, (await guest.PatchAsJsonAsync($"/api/users/{author}/follow/notifications", new { notifyOnNewPost = true })).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await client.PatchAsJsonAsync($"/api/users/{author}/follow/notifications", new { })).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await client.PatchAsJsonAsync($"/api/users/{author}/follow/notifications", new { notifyOnNewPost = true })).StatusCode);
        var own = await client.GetFromJsonAsync<JsonElement>($"/api/users/{author}/follow-status");
        Assert.True(own.GetProperty("notifyOnNewPost").GetBoolean());
        await using var verify = fixture.Context();
        var notice = Assert.Single(await verify.Set<Notification>().Where(n => n.EventKey == $"follow:{actor}:{author}").ToListAsync());
        Assert.Equal(author, notice.UserId); Assert.Equal($"/users/{actor}", notice.TargetUrl);
    }

    [Fact]
    public async Task ConcurrentFollowAndRefollow_CreateOneNotification()
    {
        var author = await UserAsync(); var actor = await UserAsync();
        using var first = fixture.Factory.Client(actor); using var second = fixture.Factory.Client(actor);
        var responses = await Task.WhenAll(first.PostAsync($"/api/users/{author}/follow", null), second.PostAsync($"/api/users/{author}/follow", null));
        Assert.Equal(1, responses.Count(r => r.StatusCode == HttpStatusCode.OK));
        Assert.Equal(1, responses.Count(r => r.StatusCode == HttpStatusCode.Conflict));
        await using (var db = fixture.Context()) await Follows(db).UnfollowAsync(actor, author);
        await FollowAsync(actor, author, false);
        await using var verify = fixture.Context();
        Assert.Equal(1, await verify.UserFollows.CountAsync(f => f.FollowerId == actor && f.FollowingId == author));
        Assert.Equal(1, await verify.Set<Notification>().CountAsync(n => n.EventKey == $"follow:{actor}:{author}"));
    }

    [Fact]
    public async Task NewPost_CapturesOnlyCurrentOptIns_NoBackfillOrEditEvents()
    {
        var author = await UserAsync(); var on = await UserAsync(); var off = await UserAsync(); var late = await UserAsync();
        await FollowAsync(on, author, true); await FollowAsync(off, author, false);
        var post = await PostAsync(author);
        await FollowAsync(late, author, true);
        await using (var db = fixture.Context()) await Follows(db).SetNewPostNotificationsAsync(off, author, true);
        await DispatchAsync(); await DispatchAsync();
        await using (var db = fixture.Context()) await db.Posts.Where(p => p.Id == post).ExecuteUpdateAsync(u => u.SetProperty(p => p.Title, "Edited title"));
        await DispatchAsync();
        await using var verify = fixture.Context();
        var notice = Assert.Single(await verify.Set<Notification>().Where(n => n.SourcePostId == post).ToListAsync());
        Assert.Equal(on, notice.UserId); Assert.Equal("followed_post", notice.Type);
        Assert.False(await verify.Set<PostFollowerNotification>().AnyAsync(n => n.PostId == post));
    }

    [Theory]
    [InlineData("off")]
    [InlineData("unfollow")]
    [InlineData("hidden")]
    [InlineData("deleted")]
    public async Task PendingNewPost_SuppressedWhenEligibilityChanges(string change)
    {
        var author = await UserAsync(); var actor = await UserAsync();
        await FollowAsync(actor, author, true); var post = await PostAsync(author);
        await using (var db = fixture.Context())
        {
            if (change == "off") await Follows(db).SetNewPostNotificationsAsync(actor, author, false);
            if (change == "unfollow") await Follows(db).UnfollowAsync(actor, author);
            if (change == "hidden") await db.Posts.Where(p => p.Id == post).ExecuteUpdateAsync(u => u.SetProperty(p => p.Status, "pending_review"));
            if (change == "deleted") await db.Posts.Where(p => p.Id == post).ExecuteUpdateAsync(u => u.SetProperty(p => p.IsDeleted, true).SetProperty(p => p.DeletedAt, DateTime.UtcNow));
        }
        if (change == "off") { await using var db = fixture.Context(); await Follows(db).SetNewPostNotificationsAsync(actor, author, true); }
        if (change == "unfollow") await FollowAsync(actor, author, true);
        await DispatchAsync();
        await using var verify = fixture.Context();
        Assert.False(await verify.Set<Notification>().AnyAsync(n => n.SourcePostId == post));
    }

    [Fact]
    public async Task CaptureWhileTurningOff_DoesNotDeliverObsoleteAudienceAfterReenable()
    {
        var author = await UserAsync(); var actor = await UserAsync(); await FollowAsync(actor, author, true);
        await using var offDb = fixture.Context();
        var follows = new FollowRepository(offDb);
        await using var tx = await follows.BeginTransactionAsync(actor, author);
        var follow = (await follows.FindAsync(actor, author))!;
        follow.NotifyOnNewPost = false; await follows.SaveChangesAsync();
        var creation = PostAsync(author);
        await using var observer = fixture.Context();
        var waiting = false;
        for (var i = 0; i < 100 && !waiting; i++)
        {
            waiting = await observer.Database.SqlQueryRaw<bool>("SELECT EXISTS (SELECT 1 FROM pg_stat_activity WHERE datname = current_database() AND wait_event_type = 'Lock' AND query LIKE '%post_follower_notifications%') AS \"Value\"").SingleAsync();
            if (!waiting) await Task.Delay(50);
        }
        Assert.True(waiting, "Post creation must reach the contested follow lock.");
        await follows.CancelNewPostDeliveriesAsync(follow.Id);
        await tx.CommitAsync();
        var post = await creation;
        await using (var db = fixture.Context()) await Follows(db).SetNewPostNotificationsAsync(actor, author, true);
        await DispatchAsync();
        await using var verify = fixture.Context();
        Assert.False(await verify.Set<Notification>().AnyAsync(n => n.SourcePostId == post));
    }

    [Fact]
    public async Task ClaimedFanout_HideAndApproveWhileWaitingDoesNotReviveDeletedQueue()
    {
        var author = await UserAsync(); var actor = await UserAsync(); await FollowAsync(actor, author, true);
        var post = await PostAsync(author);
        await using var visibility = fixture.Context();
        await using var tx = await visibility.Database.BeginTransactionAsync();
        await visibility.Database.ExecuteSqlInterpolatedAsync($"SELECT id FROM posts WHERE id = {post} FOR UPDATE");
        var dispatch = DispatchAsync();
        var waiting = false;
        await using var observer = fixture.Context();
        for (var i = 0; i < 100 && !waiting; i++)
        {
            waiting = await observer.Database.SqlQueryRaw<bool>("SELECT EXISTS (SELECT 1 FROM pg_stat_activity WHERE datname = current_database() AND wait_event_type = 'Lock' AND query LIKE '%FOR SHARE%' AND query LIKE '%FROM posts%') AS \"Value\"").SingleAsync();
            if (!waiting) await Task.Delay(50);
        }
        Assert.True(waiting, "Fan-out must reach the contested source-post lock.");
        await visibility.Posts.Where(p => p.Id == post).ExecuteUpdateAsync(u => u.SetProperty(p => p.Status, "pending_review"));
        await visibility.Posts.Where(p => p.Id == post).ExecuteUpdateAsync(u => u.SetProperty(p => p.Status, "published"));
        await tx.CommitAsync();
        await dispatch;
        await using var verify = fixture.Context();
        Assert.False(await verify.Set<Notification>().AnyAsync(n => n.SourcePostId == post));
    }

    [Fact]
    public async Task HideThenApprove_DoesNotRevivePendingFanoutOrPush()
    {
        var author = await UserAsync(); var actor = await UserAsync(); await FollowAsync(actor, author, true);
        var post = await PostAsync(author);
        await using (var db = fixture.Context())
        {
            await db.Posts.Where(p => p.Id == post).ExecuteUpdateAsync(u => u.SetProperty(p => p.Status, "pending_review"));
            await db.Posts.Where(p => p.Id == post).ExecuteUpdateAsync(u => u.SetProperty(p => p.Status, "published"));
        }
        await DispatchAsync();
        await using var verify = fixture.Context();
        Assert.False(await verify.Set<Notification>().AnyAsync(n => n.SourcePostId == post));
    }

    [Fact]
    public async Task BatchesAndConcurrentWorkers_DoNotDuplicateOrLoseAudience()
    {
        await using (var drain = fixture.Context())
            while (await new FollowerPostNotificationDispatcher(new NotificationRepository(drain)).DispatchBatchAsync() > 0) { }
        var author = await UserAsync();
        for (var i = 0; i < 23; i++) await FollowAsync(await UserAsync(), author, true);
        var post = await PostAsync(author);
        await using (var db = fixture.Context()) Assert.Equal(20, await new FollowerPostNotificationDispatcher(new NotificationRepository(db)).DispatchBatchAsync());
        await Task.WhenAll(DispatchAsync(), DispatchAsync());
        await using var verify = fixture.Context();
        Assert.Equal(23, await verify.Set<Notification>().CountAsync(n => n.SourcePostId == post));
        Assert.False(await verify.Set<PostFollowerNotification>().AnyAsync(n => n.PostId == post));
    }

    [Theory]
    [InlineData("off")]
    [InlineData("hide")]
    public async Task PreferenceOffOrPostHide_PreservesInboxAndCancelsPendingPush(string change)
    {
        var author = await UserAsync(); var actor = await UserAsync(); await FollowAsync(actor, author, true);
        await using (var db = fixture.Context()) await new NotificationRepository(db).SubscribeAsync(new BrowserPushSubscription
        { UserId = actor, Endpoint = "https://fcm.googleapis.com/test/" + Guid.NewGuid(), EndpointHash = Guid.NewGuid().ToString("N"), P256dh = "test", Auth = "test", UpdatedAt = DateTime.UtcNow, IsActive = true }, default);
        var post = await PostAsync(author); await DispatchAsync();
        await using (var db = fixture.Context())
        {
            if (change == "off") await Follows(db).SetNewPostNotificationsAsync(actor, author, false);
            else
            {
                await db.Posts.Where(p => p.Id == post).ExecuteUpdateAsync(u => u.SetProperty(p => p.Status, "pending_review"));
                await db.Posts.Where(p => p.Id == post).ExecuteUpdateAsync(u => u.SetProperty(p => p.Status, "published"));
            }
        }
        await using var verify = fixture.Context();
        var notice = await verify.Set<Notification>().SingleAsync(n => n.SourcePostId == post);
        Assert.Equal("cancelled", (await verify.Set<NotificationPushDelivery>().SingleAsync(d => d.NotificationId == notice.Id)).State);
        Assert.False(await new NotificationRepository(verify).IsPushClaimCurrentAsync(new NotificationPushDelivery { Id = (await verify.Set<NotificationPushDelivery>().SingleAsync(d => d.NotificationId == notice.Id)).Id, Notification = notice }, default));
        Assert.True(await verify.UserFollows.AnyAsync(f => f.FollowerId == actor && f.FollowingId == author));
    }

    [Fact]
    public async Task DisableWaitsForInFlightNewPostPush()
    {
        var author = await UserAsync(); var actor = await UserAsync(); await FollowAsync(actor, author, true);
        await using (var db = fixture.Context()) await new NotificationRepository(db).SubscribeAsync(new BrowserPushSubscription
        { UserId = actor, Endpoint = "https://fcm.googleapis.com/test/" + Guid.NewGuid(), EndpointHash = Guid.NewGuid().ToString("N"), P256dh = "test", Auth = "test", UpdatedAt = DateTime.UtcNow, IsActive = true }, default);
        var post = await PostAsync(author); await DispatchAsync();
        var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var transport = new Mock<IWebPushTransport>();
        transport.Setup(t => t.SendAsync(It.IsAny<BrowserPushSubscription>(), It.IsAny<Notification>(), It.IsAny<WebPushSettings>(), It.IsAny<CancellationToken>()))
            .Returns<BrowserPushSubscription, Notification, WebPushSettings, CancellationToken>(async (_, n, _, ct) =>
            {
                if (n.SourcePostId == post) { entered.TrySetResult(); await release.Task.WaitAsync(ct); }
                return 201;
            });
        await using var pushDb = fixture.Context();
        var sending = new NotificationPushDispatcher(new NotificationRepository(pushDb), transport.Object,
            Options.Create(new WebPushSettings { Enabled = true }), TimeProvider.System, NullLogger<NotificationPushDispatcher>.Instance).DispatchAsync(default);
        try
        {
            await entered.Task.WaitAsync(TimeSpan.FromSeconds(10));
            async Task Disable() { await using var db = fixture.Context(); await Follows(db).SetNewPostNotificationsAsync(actor, author, false); }
            var disabling = Disable();
            var waiting = false;
            await using var observer = fixture.Context();
            for (var i = 0; i < 100 && !waiting; i++)
            {
                waiting = await observer.Database.SqlQueryRaw<bool>("SELECT EXISTS (SELECT 1 FROM pg_stat_activity WHERE datname = current_database() AND wait_event_type = 'Lock' AND query LIKE '%user_follows%' AND query LIKE '%FOR UPDATE%') AS \"Value\"").SingleAsync();
                if (!waiting) await Task.Delay(50);
            }
            Assert.True(waiting, "Disable must serialize with the send already in progress.");
            release.TrySetResult(); await sending; await disabling;
        }
        finally { release.TrySetResult(); }
        transport.Verify(t => t.SendAsync(It.IsAny<BrowserPushSubscription>(), It.Is<Notification>(n => n.SourcePostId == post), It.IsAny<WebPushSettings>(), It.IsAny<CancellationToken>()), Times.Once);
        await using var verify = fixture.Context();
        var notification = await verify.Set<Notification>().SingleAsync(n => n.SourcePostId == post);
        Assert.Equal("sent", (await verify.Set<NotificationPushDelivery>().SingleAsync(d => d.NotificationId == notification.Id)).State);
        Assert.False(await new NotificationRepository(verify).NewPostPushAllowedAsync(notification, default));
    }

    [Fact]
    public async Task PostAndNotificationActions_RollBackAtomically()
    {
        var author = await UserAsync(); var actor = await UserAsync(); await FollowAsync(actor, author, true);
        long rolledBack;
        await using (var db = fixture.Context())
        {
            var repo = new PostRepository(db); await repo.BeginTransactionAsync();
            rolledBack = (await repo.CreatePostAsync(new Post { AuthorId = author, Title = "Rollback", Status = "published", CreatedAt = DateTime.UtcNow })).Id;
            await repo.RollbackTransactionAsync();
        }
        var post = await PostAsync(author);
        var publisher = new Mock<INotificationPublisher>();
        publisher.Setup(p => p.NotifyPostLikeAsync(actor, post, default)).ThrowsAsync(new InvalidOperationException("Test publication failure"));
        await using (var db = fixture.Context())
            await Assert.ThrowsAsync<InvalidOperationException>(() => new PostInteractionService(new PostInteractionRepository(db), publisher.Object).ToggleLikeAsync(actor, post));
        publisher.Setup(p => p.NotifyFollowAsync(author, actor, default)).ThrowsAsync(new InvalidOperationException("Test follow notification failure"));
        await using (var db = fixture.Context())
            await Assert.ThrowsAsync<InvalidOperationException>(() => new FollowService(new FollowRepository(db), publisher.Object).FollowAsync(author, actor));
        publisher.Setup(p => p.NotifyCommentAsync(It.IsAny<long>(), default)).ThrowsAsync(new InvalidOperationException("Test comment notification failure"));
        await using (var db = fixture.Context())
            await Assert.ThrowsAsync<InvalidOperationException>(() => new CommentService(new CommentRepository(db), new CreateCommentRequestValidator(),
                new UpdateCommentRequestValidator(), new ReportCommentRequestValidator(), new CommentKeywordFilter(), publisher.Object)
                .CreateCommentAsync(post, actor, new CreateCommentRequest { Content = "Rollback comment" }));
        await using var verify = fixture.Context();
        Assert.False(await verify.UserFollows.AnyAsync(f => f.FollowerId == author && f.FollowingId == actor));
        Assert.False(await verify.Comments.AnyAsync(c => c.PostId == post));
        Assert.False(await verify.Posts.AnyAsync(p => p.Id == rolledBack));
        Assert.False(await verify.Set<PostFollowerNotification>().AnyAsync(p => p.PostId == rolledBack));
        Assert.False(await verify.PostLikes.AnyAsync(l => l.PostId == post && l.UserId == actor));
    }

    [Fact]
    public async Task FanoutQueue_RejectsDirectBrowserAccess()
    {
        await using var db = fixture.Context();
        var connection = (NpgsqlConnection)db.Database.GetDbConnection(); await connection.OpenAsync();
        await using var command = new NpgsqlCommand("""
            SELECT relrowsecurity AND NOT has_table_privilege('anon', oid, 'SELECT')
                AND NOT has_table_privilege('authenticated', oid, 'INSERT')
            FROM pg_class WHERE oid = 'public.post_follower_notifications'::regclass
            """, connection);
        Assert.Equal(true, await command.ExecuteScalarAsync());
    }
}
