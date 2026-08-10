using DocumentChatbot.Core.Application.Models;
using DocumentChatbot.Core.Domain;

namespace DocumentChatbot.Core.Application.Abstractions;

public interface IChatService
{
    Task<ChatSession> CreateSessionAsync(
        string userId,
        CancellationToken cancellationToken = default);

    Task<ChatSession> GetSessionAsync(
        string userId,
        Guid sessionId,
        CancellationToken cancellationToken = default);

    Task<AskQuestionResult> AskAsync(
        string userId,
        Guid sessionId,
        string question,
        CancellationToken cancellationToken = default);
}
