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
        var gemini = new Mock<IGeminiChatClient>();
        gemini.Setup(item => item.CompleteWithPostSearchAsync(It.IsAny<string?>(),
                It.IsAny<IReadOnlyList<AiChatTurn>>(),
                It.IsAny<Func<AiPostSearchCriteria, CancellationToken, Task<AiPostSearchResult>>>(),
                It.IsAny<CancellationToken>()))
            .Returns<string?, IReadOnlyList<AiChatTurn>, Func<AiPostSearchCriteria, CancellationToken, Task<AiPostSearchResult>>, CancellationToken>(
                async (_, _, search, ct) =>
                {
                    var result = await search(new AiPostSearchCriteria(["mushroom"], null, ["Sesame"], "lunch", null, null, false), ct);
                    return new GeminiChatCompletion("Try this recipe.", "test", 1, 1)
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
            Mock.Of<IGeminiChatClient>(), NullLogger<AiChatService>.Instance);

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

        var gemini = new Mock<IGeminiChatClient>();
        gemini
            .Setup(item => item.CompleteWithPostSearchAsync(
                It.IsAny<string?>(),
                It.IsAny<IReadOnlyList<AiChatTurn>>(),
                It.IsAny<Func<AiPostSearchCriteria, CancellationToken, Task<AiPostSearchResult>>>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new GeminiChatCompletion("Gợi ý món chay.", "gemini-2.5-flash-lite", 12, 8));

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
                usage.FeatureType == "chatbot" && usage.Provider == "gemini" &&
                usage.GuestSessionId == guestId && usage.Status == "completed" &&
                usage.ModelName == "gemini-2.5-flash-lite" && usage.InputTokens == 12 &&
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
        var gemini = new Mock<IGeminiChatClient>();
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
        var gemini = new Mock<IGeminiChatClient>();
        gemini.Setup(item => item.CompleteWithPostSearchAsync(It.IsAny<string?>(),
                It.IsAny<IReadOnlyList<AiChatTurn>>(),
                It.IsAny<Func<AiPostSearchCriteria, CancellationToken, Task<AiPostSearchResult>>>(),
                It.IsAny<CancellationToken>()))
            .Callback<string?, IReadOnlyList<AiChatTurn>, Func<AiPostSearchCriteria, CancellationToken, Task<AiPostSearchResult>>, CancellationToken>(
                (context, _, _, _) => capturedContext = context)
            .ReturnsAsync(new GeminiChatCompletion("Try tofu.", "test", 1, 1));
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
        var gemini = new Mock<IGeminiChatClient>();
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
            Mock.Of<IGeminiChatClient>(),
            NullLogger<AiChatService>.Instance);

        var result = await service.GetHistoryAsync(null, 42, null);

        Assert.False(result.Succeeded);
        Assert.Equal(400, result.StatusCode);
    }
}
