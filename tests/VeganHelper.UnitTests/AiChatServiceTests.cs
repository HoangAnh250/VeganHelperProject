using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using VeganHelper.BLL.Contracts;
using VeganHelper.BLL.DTOs.AI;
using VeganHelper.BLL.Services;
using VeganHelper.DAL.Entities;
using VeganHelper.DAL.Repositories;

namespace VeganHelper.UnitTests;

public sealed class AiChatServiceTests
{
    [Fact]
    public async Task SendAsync_PostSearchExcludesPreviousCardsAndDeclaredAllergies()
    {
        var session = new ChatSession { Id = 9, UserId = 7 };
        var repository = new Mock<IChatRepository>();
        repository.Setup(item => item.FindSessionAsync(9, 7, null, It.IsAny<CancellationToken>()))
            .ReturnsAsync(session);
        repository.Setup(item => item.GetMessagesAsync(9, 20, It.IsAny<CancellationToken>()))
            .ReturnsAsync([]);
        repository.Setup(item => item.GetRecommendedPostIdsAsync(9, It.IsAny<CancellationToken>()))
            .ReturnsAsync([12]);
        var posts = new Mock<IPostRepository>();
        posts.Setup(item => item.SearchRecommendationsAsync(It.IsAny<PostRecommendationQuery>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(([new PostRecommendationProjection(42, "Mushroom tofu", null)], false));
        var health = new Mock<IHealthProfileRepository>();
        health.Setup(item => item.GetAsync(7, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new HealthProfileProjection(new UserProfile { UserId = 7, DietType = "vegan" },
                [new AllergyProjection { Name = "Peanut" }]));
        var gemini = new Mock<IAiChatClient>();
        gemini.Setup(item => item.CompleteWithPostSearchAsync(It.IsAny<string?>(),
                It.IsAny<IReadOnlyList<AiChatTurn>>(),
                It.IsAny<Func<AiPostSearchCriteria, CancellationToken, Task<AiPostSearchResult>>>(),
                It.IsAny<CancellationToken>()))
            .Returns<string?, IReadOnlyList<AiChatTurn>, Func<AiPostSearchCriteria, CancellationToken, Task<AiPostSearchResult>>, CancellationToken>(
                async (_, _, search, ct) =>
                {
                    var result = await search(new AiPostSearchCriteria(["mushroom"], null, ["Sesame"], "lunch", null, null, false), ct);
                    return new AiChatCompletion("Try this recipe.", "test", 1, 1)
                    { RecommendedPosts = result.Posts };
                });
        var service = new AiChatService(repository.Object, posts.Object, health.Object, gemini.Object,
            NullLogger<AiChatService>.Instance);

        var result = await service.SendAsync(7, new AiChatRequestDto(9, null, "More mushroom lunch ideas"));

        Assert.True(result.Succeeded);
        Assert.Equal(42, Assert.Single(result.Data!.RecommendedPosts).PostId);
        posts.Verify(item => item.SearchRecommendationsAsync(It.Is<PostRecommendationQuery>(query =>
            query.ExcludedPostIds.Contains(12) && query.Allergies.Contains("Peanut") &&
            query.Allergies.Contains("Sesame") &&
            query.DietType == "vegan" && query.MealType == "lunch"), It.IsAny<CancellationToken>()), Times.Once);
        repository.Verify(item => item.AddMessage(It.Is<ChatMessage>(message =>
            message.SenderType == "ai" && message.RecommendedPostIdsJson == "[42]")), Times.Once);
    }

    [Fact]
    public async Task GetHistoryAsync_HidesCardsWhosePostsAreNoLongerPublic()
    {
        var repository = new Mock<IChatRepository>();
        repository.Setup(item => item.FindSessionAsync(9, 7, null, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ChatSession { Id = 9, UserId = 7 });
        repository.Setup(item => item.GetMessagesAsync(9, 100, It.IsAny<CancellationToken>()))
            .ReturnsAsync([new ChatMessage { Id = 3, SessionId = 9, SenderType = "ai",
                MessageText = "Two recipes", RecommendedPostIdsJson = "[42,43]" }]);
        var posts = new Mock<IPostRepository>();
        posts.Setup(item => item.GetPublicRecommendationsAsync(It.IsAny<IReadOnlyList<long>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync([new PostRecommendationProjection(42, "Still public", null)]);
        var service = new AiChatService(repository.Object, posts.Object, Mock.Of<IHealthProfileRepository>(),
            Mock.Of<IAiChatClient>(), NullLogger<AiChatService>.Instance);

        var result = await service.GetHistoryAsync(7, 9, null);

        Assert.Equal(42, Assert.Single(Assert.Single(result.Data!.Messages).RecommendedPosts).PostId);
    }

    [Fact]
    public async Task SendAsync_CreatesGuestSessionPersistsMessagesAndUsage()
    {
        var repository = new Mock<IChatRepository>();
        repository
            .Setup(item => item.TryStartGuestChatAsync(
                It.IsAny<Guid>(), It.IsAny<ChatSession>(), It.IsAny<ChatMessage>(),
                It.IsAny<AiUsage>(), 3, It.IsAny<CancellationToken>()))
            .Callback<Guid, ChatSession, ChatMessage, AiUsage, int, CancellationToken>(
                (_, session, message, _, _, _) => { session.Id = 42; message.SessionId = 42; })
            .ReturnsAsync(0L);

        var gemini = new Mock<IAiChatClient>();
        gemini
            .Setup(item => item.CompleteWithPostSearchAsync(
                It.IsAny<string?>(),
                It.IsAny<IReadOnlyList<AiChatTurn>>(),
                It.IsAny<Func<AiPostSearchCriteria, CancellationToken, Task<AiPostSearchResult>>>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new AiChatCompletion("Gợi ý món chay.", "deepseek-flash", 12, 8));

        var service = new AiChatService(
            repository.Object,
            Mock.Of<IPostRepository>(),
            Mock.Of<IHealthProfileRepository>(),
            gemini.Object,
            NullLogger<AiChatService>.Instance);
        var guestId = Guid.NewGuid();

        var result = await service.SendAsync(
            null,
            new AiChatRequestDto(null, guestId, "  Gợi ý món chay  "));

        Assert.True(result.Succeeded);
        Assert.Equal(42, result.Data!.SessionId);
        Assert.Equal(guestId, result.Data.GuestSessionId);
        Assert.Equal("Gợi ý món chay.", result.Data.Answer);
        repository.Verify(item => item.AddUsage(It.Is<AiUsage>(usage =>
            usage.FeatureType == "chatbot")), Times.Never);
        repository.Verify(item => item.TryStartGuestChatAsync(
            guestId, It.IsAny<ChatSession>(),
            It.Is<ChatMessage>(message =>
                message.SenderType == "user" && message.MessageText == "Gợi ý món chay"),
            It.Is<AiUsage>(usage =>
                usage.FeatureType == "chatbot" && usage.Provider == "deepseek" &&
                usage.GuestSessionId == guestId && usage.Status == "completed" &&
                usage.ModelName == "deepseek-flash" && usage.InputTokens == 12 &&
                usage.OutputTokens == 8),
            3, It.IsAny<CancellationToken>()), Times.Once);
        repository.Verify(item => item.AddMessage(It.Is<ChatMessage>(message =>
            message.SenderType == "ai" && message.MessageText == "Gợi ý món chay.")), Times.Once);
        gemini.VerifyAll();
    }

    [Fact]
    public async Task SendAsync_DeniesGuestWhenReservationIsFullWithoutCallingGemini()
    {
        var repository = new Mock<IChatRepository>();
        repository.Setup(item => item.TryStartGuestChatAsync(
                It.IsAny<Guid>(), It.IsAny<ChatSession>(), It.IsAny<ChatMessage>(),
                It.IsAny<AiUsage>(), 3, It.IsAny<CancellationToken>()))
            .ReturnsAsync((long?)null);
        var gemini = new Mock<IAiChatClient>();
        var service = new AiChatService(repository.Object, Mock.Of<IPostRepository>(), Mock.Of<IHealthProfileRepository>(),
            gemini.Object, NullLogger<AiChatService>.Instance);

        var result = await service.SendAsync(null,
            new AiChatRequestDto(null, Guid.NewGuid(), "Hello"));

        Assert.Equal(429, result.StatusCode);
        Assert.Equal("GUEST_CHAT_LIMIT_REACHED", result.ErrorCode);
        gemini.Verify(item => item.CompleteWithPostSearchAsync(
            It.IsAny<string?>(), It.IsAny<IReadOnlyList<AiChatTurn>>(),
            It.IsAny<Func<AiPostSearchCriteria, CancellationToken, Task<AiPostSearchResult>>>(),
            It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task SendAsync_UsesOnlyAvailableProfileFactsForMember()
    {
        var repository = new Mock<IChatRepository>();
        repository.Setup(item => item.AddSession(It.IsAny<ChatSession>()))
            .Callback<ChatSession>(session => session.Id = 91);
        var profile = new UserProfile
        {
            UserId = 7, HeightCm = 170, WeightKg = 68,
            DietType = "lacto_ovo_vegetarian", ActivityLevel = "moderate",
            BirthDate = new DateOnly(1995, 1, 1), BiologicalSex = "female"
        };
        var health = new Mock<IHealthProfileRepository>();
        health.Setup(item => item.GetAsync(7, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new HealthProfileProjection(profile,
                [new AllergyProjection { Name = "Peanuts" }]));
        string? capturedContext = null;
        var gemini = new Mock<IAiChatClient>();
        gemini.Setup(item => item.CompleteWithPostSearchAsync(It.IsAny<string?>(),
                It.IsAny<IReadOnlyList<AiChatTurn>>(),
                It.IsAny<Func<AiPostSearchCriteria, CancellationToken, Task<AiPostSearchResult>>>(),
                It.IsAny<CancellationToken>()))
            .Callback<string?, IReadOnlyList<AiChatTurn>, Func<AiPostSearchCriteria, CancellationToken, Task<AiPostSearchResult>>, CancellationToken>(
                (context, _, _, _) => capturedContext = context)
            .ReturnsAsync(new AiChatCompletion("Try tofu.", "test", 1, 1));
        var service = new AiChatService(repository.Object, Mock.Of<IPostRepository>(), health.Object, gemini.Object,
            NullLogger<AiChatService>.Instance);

        var result = await service.SendAsync(7, new AiChatRequestDto(null, null, "Lunch idea?"));

        Assert.True(result.Succeeded);
        Assert.Contains("\"meal_type\":\"lacto_ovo_vegetarian\"", capturedContext);
        Assert.Contains("\"activity_level\":\"moderate\"", capturedContext);
        Assert.Contains("\"estimated_maintenance_calories\":", capturedContext);
        Assert.Contains("Peanuts", capturedContext);
        Assert.DoesNotContain("health_goal", capturedContext);
        Assert.Null(result.Data!.GuestQuestionsRemaining);
    }

    [Fact]
    public async Task SendAsync_RejectsEmptyOrOverlongMessageBeforeCallingProvider()
    {
        var repository = new Mock<IChatRepository>();
        var gemini = new Mock<IAiChatClient>();
        var service = new AiChatService(
            repository.Object,
            Mock.Of<IPostRepository>(),
            Mock.Of<IHealthProfileRepository>(),
            gemini.Object,
            NullLogger<AiChatService>.Instance);

        var empty = await service.SendAsync(null, new AiChatRequestDto(null, null, " "));
        var longMessage = await service.SendAsync(null, new AiChatRequestDto(null, null, new string('x', 4_001)));

        Assert.False(empty.Succeeded);
        Assert.Equal(400, empty.StatusCode);
        Assert.False(longMessage.Succeeded);
        Assert.Equal(400, longMessage.StatusCode);
        gemini.Verify(item => item.CompleteWithPostSearchAsync(
            It.IsAny<string?>(),
            It.IsAny<IReadOnlyList<AiChatTurn>>(),
            It.IsAny<Func<AiPostSearchCriteria, CancellationToken, Task<AiPostSearchResult>>>(),
            It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task GetHistoryAsync_DoesNotReturnSessionWithoutOwnershipProof()
    {
        var repository = new Mock<IChatRepository>();
        repository
            .Setup(item => item.FindSessionAsync(42, null, null, It.IsAny<CancellationToken>()))
            .ReturnsAsync((ChatSession?)null);
        var service = new AiChatService(
            repository.Object,
            Mock.Of<IPostRepository>(),
            Mock.Of<IHealthProfileRepository>(),
            Mock.Of<IAiChatClient>(),
            NullLogger<AiChatService>.Instance);

        var result = await service.GetHistoryAsync(null, 42, null);

        Assert.False(result.Succeeded);
        Assert.Equal(400, result.StatusCode);
    }

    [Theory]
    [InlineData(true, true)]
    [InlineData(true, false)]
    [InlineData(false, true)]
    [InlineData(false, false)]
    public async Task SendAsync_ClientCancellationReleasesPendingUsageAndDoesNotSaveAnAnswer(bool guest, bool providerHonorsCancellation)
    {
        using var cancellation = new CancellationTokenSource();
        AiUsage? usage = null;
        var repository = new Mock<IChatRepository>();
        repository.Setup(item => item.AddSession(It.IsAny<ChatSession>()))
            .Callback<ChatSession>(session => session.Id = 42);
        repository.Setup(item => item.AddUsage(It.IsAny<AiUsage>())).Callback<AiUsage>(value => usage = value);
        repository.Setup(item => item.TryStartGuestChatAsync(It.IsAny<Guid>(), It.IsAny<ChatSession>(),
                It.IsAny<ChatMessage>(), It.IsAny<AiUsage>(), 3, It.IsAny<CancellationToken>()))
            .Callback<Guid, ChatSession, ChatMessage, AiUsage, int, CancellationToken>((_, session, _, value, _, _) =>
            { session.Id = 42; usage = value; }).ReturnsAsync(0L);
        var client = new Mock<IAiChatClient>();
        client.Setup(item => item.CompleteWithPostSearchAsync(It.IsAny<string?>(), It.IsAny<IReadOnlyList<AiChatTurn>>(),
                It.IsAny<Func<AiPostSearchCriteria, CancellationToken, Task<AiPostSearchResult>>>(), It.IsAny<CancellationToken>()))
            .Returns(() =>
            {
                cancellation.Cancel();
                return providerHonorsCancellation
                    ? Task.FromCanceled<AiChatCompletion>(cancellation.Token)
                    : Task.FromResult(new AiChatCompletion("Late answer", "test", 1, 1));
            });
        var service = new AiChatService(repository.Object, Mock.Of<IPostRepository>(), Mock.Of<IHealthProfileRepository>(),
            client.Object, NullLogger<AiChatService>.Instance);

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => service.SendAsync(guest ? null : 7,
            new AiChatRequestDto(null, guest ? Guid.NewGuid() : null, "Lunch idea"), cancellation.Token));

        Assert.NotNull(usage);
        Assert.Equal("failed", usage.Status);
        repository.Verify(item => item.SaveChangesAsync(It.Is<CancellationToken>(token => !token.IsCancellationRequested)), Times.AtLeastOnce);
        repository.Verify(item => item.AddMessage(It.Is<ChatMessage>(message => message.SenderType == "ai")), Times.Never);
    }
}
