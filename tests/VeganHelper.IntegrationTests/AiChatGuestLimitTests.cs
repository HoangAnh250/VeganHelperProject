using Microsoft.EntityFrameworkCore;
using VeganHelper.DAL.Entities;
using VeganHelper.DAL.Repositories;

namespace VeganHelper.IntegrationTests;

[Collection("Member2 API")]
public sealed class AiChatGuestLimitTests(Member2ApiFixture fixture)
{
    [Fact]
    public async Task ConcurrentGuestReservations_AllowOnlyOneAvailableQuestion()
    {
        var guestId = Guid.NewGuid();

        async Task<long?> Reserve()
        {
            await using var db = fixture.Context();
            var repository = new ChatRepository(db);
            var now = DateTime.UtcNow;
            return await repository.TryStartGuestChatAsync(
                guestId,
                new ChatSession { GuestSessionId = guestId, StartedAt = now, LastActivityAt = now },
                new ChatMessage { SenderType = "user", MessageText = "Lunch?", CreatedAt = now },
                new AiUsage
                {
                    RequestId = Guid.NewGuid(), GuestSessionId = guestId,
                    FeatureType = "chatbot", Provider = "gemini",
                    ModelName = "pending", Status = "pending", CreatedAt = now
                },
                maximumQuestions: 1);
        }

        var attempts = await Task.WhenAll(Reserve(), Reserve());

        Assert.Single(attempts, result => result == 0);
        Assert.Single(attempts, result => result is null);
        await using var verify = fixture.Context();
        Assert.Equal(1, await verify.AiUsage.CountAsync(item => item.GuestSessionId == guestId));
    }
}
