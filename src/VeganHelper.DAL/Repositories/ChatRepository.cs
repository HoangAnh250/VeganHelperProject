using Microsoft.EntityFrameworkCore;
using VeganHelper.DAL.Entities;
using VeganHelper.DAL.Persistence;

namespace VeganHelper.DAL.Repositories;

public sealed class ChatRepository(AppDbContext db) : IChatRepository
{
    public Task<ChatSession?> FindSessionAsync(
        long sessionId,
        long? userId,
        Guid? guestSessionId,
        CancellationToken cancellationToken = default)
    {
        var query = db.ChatSessions.Where(session => session.Id == sessionId);

        if (userId.HasValue)
        {
            query = query.Where(session => session.UserId == userId && session.GuestSessionId == null);
        }
        else if (guestSessionId.HasValue)
        {
            query = query.Where(session => session.UserId == null && session.GuestSessionId == guestSessionId);
        }
        else
        {
            return Task.FromResult<ChatSession?>(null);
        }

        return query.SingleOrDefaultAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<ChatMessage>> GetMessagesAsync(
        long sessionId,
        int maximum,
        CancellationToken cancellationToken = default)
    {
        var messages = await db.ChatMessages
            .AsNoTracking()
            .Where(message => message.SessionId == sessionId)
            .OrderByDescending(message => message.Id)
            .Take(maximum)
            .ToListAsync(cancellationToken);

        messages.Reverse();
        return messages;
    }

    public Task<long> CountCompletedGuestChatRequestsAsync(
        Guid guestSessionId,
        CancellationToken cancellationToken = default) =>
        db.AiUsage
            .AsNoTracking()
            .LongCountAsync(usage =>
                usage.GuestSessionId == guestSessionId &&
                usage.UserId == null &&
                usage.FeatureType == "chatbot" &&
                usage.Status == "completed",
                cancellationToken);

    public void AddSession(ChatSession session) => db.ChatSessions.Add(session);

    public void AddMessage(ChatMessage message) => db.ChatMessages.Add(message);

    public void AddUsage(AiUsage usage) => db.AiUsage.Add(usage);

    public Task SaveChangesAsync(CancellationToken cancellationToken = default) =>
        db.SaveChangesAsync(cancellationToken);
}
