using VeganHelper.DAL.Entities;

namespace VeganHelper.DAL.Repositories;

public interface IChatRepository
{
    Task<ChatSession?> FindSessionAsync(
        long sessionId,
        long? userId,
        Guid? guestSessionId,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<ChatMessage>> GetMessagesAsync(
        long sessionId,
        int maximum,
        CancellationToken cancellationToken = default);

    Task<long?> TryStartGuestChatAsync(
        Guid guestSessionId,
        ChatSession session,
        ChatMessage message,
        AiUsage usage,
        int maximumQuestions,
        CancellationToken cancellationToken = default);

    void AddSession(ChatSession session);
    void AddMessage(ChatMessage message);
    void AddUsage(AiUsage usage);
    Task SaveChangesAsync(CancellationToken cancellationToken = default);
}
