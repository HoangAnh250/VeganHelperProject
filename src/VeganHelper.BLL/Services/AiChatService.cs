using Microsoft.Extensions.Logging;
using VeganHelper.BLL.Contracts;
using VeganHelper.BLL.DTOs;
using VeganHelper.BLL.DTOs.AI;
using VeganHelper.DAL.Entities;
using VeganHelper.DAL.Repositories;

namespace VeganHelper.BLL.Services;

public sealed class AiChatService(
    IChatRepository repository,
    IGeminiChatClient gemini,
    ILogger<AiChatService> logger) : IAiChatService
{
    private const int MaxMessageCharacters = 4_000;
    private const int MaxHistoryMessagesForModel = 20;
    private const int MaxHistoryMessagesForResponse = 100;

    public async Task<ServiceResult<AiChatResponseDto>> SendAsync(
        long? userId,
        AiChatRequestDto request,
        CancellationToken cancellationToken = default)
    {
        if (request is null)
            return ServiceResult<AiChatResponseDto>.Fail("A chat message is required.", 400);

        var messageText = request.Message?.Trim();
        if (string.IsNullOrWhiteSpace(messageText))
            return ServiceResult<AiChatResponseDto>.Fail("A chat message is required.", 400);

        if (messageText.Length > MaxMessageCharacters)
            return ServiceResult<AiChatResponseDto>.Fail(
                $"A chat message cannot exceed {MaxMessageCharacters} characters.", 400);

        if (userId is null && request.SessionId.HasValue && !request.GuestSessionId.HasValue)
            return ServiceResult<AiChatResponseDto>.Fail(
                "GuestSessionId is required when continuing an anonymous chat session.", 400);

        Guid? guestSessionId = userId.HasValue
            ? null
            : request.GuestSessionId ?? Guid.NewGuid();

        var now = DateTime.UtcNow;
        ChatSession? session;
        if (request.SessionId.HasValue)
        {
            session = await repository.FindSessionAsync(
                request.SessionId.Value,
                userId,
                guestSessionId,
                cancellationToken);

            if (session is null)
                return ServiceResult<AiChatResponseDto>.Fail(
                    "The chat session was not found or is not accessible.", 404);
        }
        else
        {
            session = new ChatSession
            {
                UserId = userId,
                GuestSessionId = guestSessionId,
                StartedAt = now,
                LastActivityAt = now
            };
            repository.AddSession(session);
            await repository.SaveChangesAsync(cancellationToken);
        }

        var previousMessages = await repository.GetMessagesAsync(
            session.Id,
            MaxHistoryMessagesForModel,
            cancellationToken);

        var userMessage = new ChatMessage
        {
            SessionId = session.Id,
            SenderType = "user",
            MessageText = messageText,
            CreatedAt = now
        };
        repository.AddMessage(userMessage);

        var usage = new AiUsage
        {
            RequestId = Guid.NewGuid(),
            UserId = userId,
            GuestSessionId = guestSessionId,
            FeatureType = "chatbot",
            Provider = "gemini",
            ModelName = "pending",
            Status = "pending",
            CreatedAt = now
        };
        repository.AddUsage(usage);
        await repository.SaveChangesAsync(cancellationToken);

        var turns = previousMessages
            .Select(message => new AiChatTurn(message.SenderType, message.MessageText))
            .Append(new AiChatTurn("user", messageText))
            .ToArray();

        GeminiChatCompletion completion;
        try
        {
            completion = await gemini.CompleteAsync(
                session.ContextSummary,
                turns,
                cancellationToken);
        }
        catch (GeminiChatException exception)
        {
            usage.Status = "failed";
            usage.ModelName = "gemini";
            await repository.SaveChangesAsync(cancellationToken);
            logger.LogWarning(
                "Gemini chat request {RequestId} failed with {Code}.",
                usage.RequestId,
                exception.Code);
            return ServiceResult<AiChatResponseDto>.Fail(exception.Message, exception.StatusCode);
        }
        catch (Exception exception) when (exception is HttpRequestException or IOException)
        {
            usage.Status = "failed";
            usage.ModelName = "gemini";
            await repository.SaveChangesAsync(cancellationToken);
            logger.LogWarning(exception, "Gemini chat provider was unavailable for {RequestId}.", usage.RequestId);
            return ServiceResult<AiChatResponseDto>.Fail(
                "The AI service is temporarily unavailable. Please try again later.", 503);
        }

        var aiMessage = new ChatMessage
        {
            SessionId = session.Id,
            SenderType = "ai",
            MessageText = completion.Text,
            CreatedAt = DateTime.UtcNow
        };
        repository.AddMessage(aiMessage);

        session.LastActivityAt = aiMessage.CreatedAt;
        usage.Status = "completed";
        usage.ModelName = completion.Model;
        usage.InputTokens = completion.InputTokens;
        usage.OutputTokens = completion.OutputTokens;
        await repository.SaveChangesAsync(cancellationToken);

        return ServiceResult<AiChatResponseDto>.Ok(new AiChatResponseDto(
            session.Id,
            session.GuestSessionId,
            completion.Text,
            aiMessage.CreatedAt,
            completion.Model));
    }

    public async Task<ServiceResult<AiChatHistoryDto>> GetHistoryAsync(
        long? userId,
        long sessionId,
        Guid? guestSessionId,
        CancellationToken cancellationToken = default)
    {
        if (sessionId <= 0)
            return ServiceResult<AiChatHistoryDto>.Fail("A valid session id is required.", 400);

        if (userId is null && !guestSessionId.HasValue)
            return ServiceResult<AiChatHistoryDto>.Fail(
                "GuestSessionId is required for an anonymous chat session.", 400);

        var session = await repository.FindSessionAsync(
            sessionId,
            userId,
            guestSessionId,
            cancellationToken);
        if (session is null)
            return ServiceResult<AiChatHistoryDto>.Fail(
                "The chat session was not found or is not accessible.", 404);

        var messages = await repository.GetMessagesAsync(
            session.Id,
            MaxHistoryMessagesForResponse,
            cancellationToken);

        return ServiceResult<AiChatHistoryDto>.Ok(new AiChatHistoryDto(
            session.Id,
            session.GuestSessionId,
            session.StartedAt,
            session.LastActivityAt,
            messages.Select(message => new AiChatMessageDto(
                message.Id,
                message.SenderType,
                message.MessageText,
                message.CreatedAt)).ToList()));
    }
}
