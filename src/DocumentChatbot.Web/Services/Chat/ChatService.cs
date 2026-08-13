using DocumentChatbot.Web.Exceptions;
using DocumentChatbot.Web.Models;

namespace DocumentChatbot.Web.Services;

public sealed class ChatService(
    IChatSessionRepository sessions,
    IRagService ragService,
    TimeProvider timeProvider) : IChatService
{
    public const int MaximumQuestionLength = 2_000;
    public const int HistoryMessageLimit = 8;
    public const string OutOfScopeAnswer =
        "Mình chưa tìm thấy thông tin đủ tin cậy trong tài liệu đã được lập chỉ mục để trả lời câu hỏi này.";

    public async Task<ChatSession> CreateSessionAsync(
        Guid userId,
        int courseId,
        CancellationToken cancellationToken = default)
    {
        if (userId == Guid.Empty)
        {
            throw new ArgumentException("User id is required.", nameof(userId));
        }

        var session = new ChatSession(
            Guid.NewGuid(),
            courseId,
            userId,
            "Cuộc trò chuyện mới",
            timeProvider.GetUtcNow());

        await sessions.AddAsync(session, cancellationToken);
        return session;
    }

    public async Task<ChatSession> GetSessionAsync(
        Guid userId,
        Guid sessionId,
        CancellationToken cancellationToken = default)
    {
        var session = await sessions.GetByIdAsync(sessionId, cancellationToken)
            ?? throw new ChatSessionNotFoundException(sessionId);

        EnsureOwner(session, userId);
        return session;
    }

    public Task<IReadOnlyList<ChatSession>> GetSessionsAsync(
        Guid userId,
        int courseId,
        CancellationToken cancellationToken = default)
    {
        if (userId == Guid.Empty)
        {
            throw new ArgumentException("User id is required.", nameof(userId));
        }

        return sessions.GetAllAsync(userId, courseId, cancellationToken);
    }

    public async Task<ChatSession> RenameSessionAsync(
        Guid userId,
        Guid sessionId,
        string title,
        CancellationToken cancellationToken = default)
    {
        var session = await GetSessionAsync(userId, sessionId, cancellationToken);
        session.Rename(title);
        await sessions.SaveAsync(session, cancellationToken);
        return session;
    }

    public async Task DeleteSessionAsync(
        Guid userId,
        Guid sessionId,
        CancellationToken cancellationToken = default)
    {
        var session = await GetSessionAsync(userId, sessionId, cancellationToken);
        await sessions.DeleteAsync(session.Id, cancellationToken);
    }

    public async Task<AskQuestionResult> AskAsync(
        Guid userId,
        Guid sessionId,
        string question,
        CancellationToken cancellationToken = default)
    {
        var normalizedQuestion = NormalizeQuestion(question);
        var session = await GetSessionAsync(userId, sessionId, cancellationToken);

        var history = session.Messages
            .TakeLast(HistoryMessageLimit)
            .Select(message => new ChatHistoryItem(message.Role, message.Content))
            .ToArray();

        var ragAnswer = await ragService.AnswerAsync(
            new RagQuestion(normalizedQuestion, history),
            cancellationToken);

        var validCitations = ragAnswer.Citations
            .Where(citation => citation.IsValid)
            .DistinctBy(citation => citation.ChunkId)
            .ToArray();

        var isGrounded = ragAnswer.IsGrounded &&
                         !string.IsNullOrWhiteSpace(ragAnswer.Answer) &&
                         validCitations.Length > 0;

        var now = timeProvider.GetUtcNow();
        session.SetTitleFromFirstQuestion(normalizedQuestion);
        var userMessage = new ChatMessage(
            Guid.NewGuid(),
            MessageRole.User,
            normalizedQuestion,
            now);
        var assistantMessage = new ChatMessage(
            Guid.NewGuid(),
            MessageRole.Assistant,
            isGrounded ? ragAnswer.Answer : OutOfScopeAnswer,
            now.AddTicks(1),
            isGrounded ? validCitations : []);

        session.Append(userMessage);
        session.Append(assistantMessage);
        await sessions.SaveAsync(session, cancellationToken);

        return new AskQuestionResult(
            session.Id,
            session.Title,
            userMessage,
            assistantMessage,
            isGrounded);
    }

    private static string NormalizeQuestion(string question)
    {
        var normalized = NormalizeRequired(question, nameof(question));
        if (normalized.Length > MaximumQuestionLength)
        {
            throw new ArgumentException(
                $"Question cannot exceed {MaximumQuestionLength} characters.",
                nameof(question));
        }

        return normalized;
    }

    private static string NormalizeRequired(string value, string parameterName)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new ArgumentException("A value is required.", parameterName);
        }

        return value.Trim();
    }

    private static void EnsureOwner(ChatSession session, Guid userId)
    {
        if (session.UserId != userId)
        {
            throw new ChatSessionAccessDeniedException(session.Id);
        }
    }
}
