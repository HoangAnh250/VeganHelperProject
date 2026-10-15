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
    public async Task SendAsync_CreatesGuestSessionPersistsMessagesAndUsage()
    {
        var repository = new Mock<IChatRepository>();
        repository
            .Setup(item => item.AddSession(It.IsAny<ChatSession>()))
            .Callback<ChatSession>(session => session.Id = 42);
        repository
            .Setup(item => item.GetMessagesAsync(42, It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Array.Empty<ChatMessage>());

        var gemini = new Mock<IGeminiChatClient>();
        gemini
            .Setup(item => item.CompleteAsync(
                It.IsAny<string?>(),
                It.IsAny<IReadOnlyList<AiChatTurn>>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new GeminiChatCompletion("Gợi ý món chay.", "gemini-2.5-flash-lite", 12, 8));

        var service = new AiChatService(
            repository.Object,
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
            usage.FeatureType == "chatbot" && usage.Provider == "gemini" &&
            usage.GuestSessionId == guestId && usage.Status == "completed" &&
            usage.ModelName == "gemini-2.5-flash-lite" && usage.InputTokens == 12 &&
            usage.OutputTokens == 8)), Times.Once);
        repository.Verify(item => item.AddMessage(It.Is<ChatMessage>(message =>
            message.SenderType == "user" && message.MessageText == "Gợi ý món chay")), Times.Once);
        repository.Verify(item => item.AddMessage(It.Is<ChatMessage>(message =>
            message.SenderType == "ai" && message.MessageText == "Gợi ý món chay.")), Times.Once);
        gemini.VerifyAll();
    }

    [Fact]
    public async Task SendAsync_RejectsEmptyOrOverlongMessageBeforeCallingProvider()
    {
        var repository = new Mock<IChatRepository>();
        var gemini = new Mock<IGeminiChatClient>();
        var service = new AiChatService(
            repository.Object,
            gemini.Object,
            NullLogger<AiChatService>.Instance);

        var empty = await service.SendAsync(null, new AiChatRequestDto(null, null, " "));
        var longMessage = await service.SendAsync(null, new AiChatRequestDto(null, null, new string('x', 4_001)));

        Assert.False(empty.Succeeded);
        Assert.Equal(400, empty.StatusCode);
        Assert.False(longMessage.Succeeded);
        Assert.Equal(400, longMessage.StatusCode);
        gemini.Verify(item => item.CompleteAsync(
            It.IsAny<string?>(),
            It.IsAny<IReadOnlyList<AiChatTurn>>(),
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
            Mock.Of<IGeminiChatClient>(),
            NullLogger<AiChatService>.Instance);

        var result = await service.GetHistoryAsync(null, 42, null);

        Assert.False(result.Succeeded);
        Assert.Equal(400, result.StatusCode);
    }
}
