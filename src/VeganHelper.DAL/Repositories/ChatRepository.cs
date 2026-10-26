using System.Text.Json;
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

    public async Task<IReadOnlyList<long>> GetRecommendedPostIdsAsync(
        long sessionId, CancellationToken cancellationToken = default)
    {
        var snapshots = await db.ChatMessages.AsNoTracking()
            .Where(message => message.SessionId == sessionId && message.RecommendedPostIdsJson != null)
            .Select(message => message.RecommendedPostIdsJson!)
            .ToListAsync(cancellationToken);
        return snapshots.SelectMany(snapshot => JsonSerializer.Deserialize<long[]>(snapshot) ?? [])
            .Where(id => id > 0).Distinct().Take(1_000).ToArray();
    }

    public async Task<long?> TryStartGuestChatAsync(
        Guid guestSessionId,
        ChatSession session,
        ChatMessage message,
        AiUsage usage,
        int maximumQuestions,
        CancellationToken cancellationToken = default)
    {
        await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);
        var lockId = BitConverter.ToInt32(guestSessionId.ToByteArray());
        await db.Database.ExecuteSqlInterpolatedAsync(
            $"SELECT pg_advisory_xact_lock({3913035}, {lockId})",
            cancellationToken);

        var recentPending = DateTime.UtcNow.AddMinutes(-3);
        var reserved = await db.AiUsage.AsNoTracking().LongCountAsync(
            item => item.GuestSessionId == guestSessionId &&
                item.UserId == null && item.FeatureType == "chatbot" &&
                (item.Status == "completed" ||
                 item.Status == "pending" && item.CreatedAt >= recentPending),
            cancellationToken);
        if (reserved >= maximumQuestions)
            return null;

        if (session.Id == 0)
        {
            db.ChatSessions.Add(session);
            await db.SaveChangesAsync(cancellationToken);
        }

        message.SessionId = session.Id;
        db.ChatMessages.Add(message);
        db.AiUsage.Add(usage);
        await db.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return reserved;
    }

    public void AddSession(ChatSession session) => db.ChatSessions.Add(session);

    public void AddMessage(ChatMessage message) => db.ChatMessages.Add(message);

    public void AddUsage(AiUsage usage) => db.AiUsage.Add(usage);

    public Task SaveChangesAsync(CancellationToken cancellationToken = default) =>
        db.SaveChangesAsync(cancellationToken);
}
