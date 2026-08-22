using DocumentChatbot.Web.Models;

namespace DocumentChatbot.Web.Contracts;

public sealed record AskQuestionRequest(string Question);

public sealed record RenameChatSessionRequest(string Title);

public sealed record ChatStatusResponse(string State, string Message);

public sealed record ChatErrorResponse(string Message);

public sealed record ChatCitationResponse(
    int Label,
    int? ChunkIndex,
    string DocumentName,
    string? Chapter,
    int? PageNumber,
    int? SlideNumber,
    string Excerpt)
{
    public static ChatCitationResponse From(Citation citation, int index) => new(
        index + 1,
        citation.ChunkIndex,
        citation.DocumentName,
        citation.Chapter,
        citation.PageNumber,
        citation.SlideNumber,
        citation.Excerpt);
}

public sealed record ChatMessageResponse(
    Guid Id,
    string Role,
    string Content,
    DateTimeOffset SentAtUtc,
    IReadOnlyList<ChatCitationResponse> Citations)
{
    public static ChatMessageResponse From(ChatMessage message) => new(
        message.Id,
        message.Role.ToString().ToLowerInvariant(),
        message.Content,
        message.SentAtUtc,
        message.Citations
            .Select(ChatCitationResponse.From)
            .ToArray());
}

public sealed record ChatSessionResponse(
    Guid Id,
    string Title,
    DateTimeOffset CreatedAtUtc,
    DateTimeOffset UpdatedAtUtc,
    IReadOnlyList<ChatMessageResponse> Messages)
{
    public static ChatSessionResponse From(ChatSession session) => new(
        session.Id,
        session.Title,
        session.CreatedAtUtc,
        session.UpdatedAtUtc,
        session.Messages.Select(ChatMessageResponse.From).ToArray());
}

public sealed record AskQuestionResponse(
    Guid SessionId,
    string SessionTitle,
    ChatMessageResponse UserMessage,
    ChatMessageResponse AssistantMessage,
    bool IsGrounded)
{
    public static AskQuestionResponse From(AskQuestionResult result) => new(
        result.SessionId,
        result.SessionTitle,
        ChatMessageResponse.From(result.UserMessage),
        ChatMessageResponse.From(result.AssistantMessage),
        result.IsGrounded);
}
