using System.Collections.Concurrent;
using DocumentChatbot.Core.Application.Abstractions;
using DocumentChatbot.Core.Domain;

namespace DocumentChatbot.Infrastructure.Persistence;

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

    public Task SaveAsync(ChatSession session, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        _sessions[session.Id] = session;
        return Task.CompletedTask;
    }
}
