using System.Collections.Concurrent;
using DocumentChatbot.Web.Services;
using DocumentChatbot.Web.Models;

namespace DocumentChatbot.Web.Services;

public sealed class InMemoryChatSessionRepository : IChatSessionRepository
{
    private readonly ConcurrentDictionary<Guid, ChatSession> _sessions = new();

    public Task AddAsync(ChatSession session, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (!_sessions.TryAdd(session.Id, session))
        {
            throw new InvalidOperationException($"Chat session '{session.Id}' already exists.");
        }

        return Task.CompletedTask;
    }

    public Task<ChatSession?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        _sessions.TryGetValue(id, out var session);
        return Task.FromResult(session);
    }

    public Task<IReadOnlyList<ChatSession>> GetAllAsync(
        Guid userId,
        int courseId,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        IReadOnlyList<ChatSession> sessions = _sessions.Values
            .Where(session => session.UserId == userId && session.CourseId == courseId)
            .OrderByDescending(session => session.UpdatedAtUtc)
            .ToArray();

        return Task.FromResult(sessions);
    }

    public Task SaveAsync(ChatSession session, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        _sessions[session.Id] = session;
        return Task.CompletedTask;
    }

    public Task DeleteAsync(Guid id, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        _sessions.TryRemove(id, out _);
        return Task.CompletedTask;
    }
}
