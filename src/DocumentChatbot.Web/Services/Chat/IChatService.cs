using DocumentChatbot.Web.Models;

namespace DocumentChatbot.Web.Services;

public interface IChatService
{
    Task<ChatSession> CreateSessionAsync(
        Guid userId,
        int courseId,
        CancellationToken cancellationToken = default);

    Task<ChatSession> GetSessionAsync(
        Guid userId,
        Guid sessionId,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<ChatSession>> GetSessionsAsync(
        Guid userId,
        int courseId,
        CancellationToken cancellationToken = default);

    Task<ChatSession> RenameSessionAsync(
        Guid userId,
        Guid sessionId,
        string title,
        CancellationToken cancellationToken = default);

    Task DeleteSessionAsync(
        Guid userId,
        Guid sessionId,
        CancellationToken cancellationToken = default);

    Task<AskQuestionResult> AskAsync(
        Guid userId,
        Guid sessionId,
        string question,
        CancellationToken cancellationToken = default);
}
