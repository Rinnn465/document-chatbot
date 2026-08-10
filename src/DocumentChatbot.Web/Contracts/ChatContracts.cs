using DocumentChatbot.Core.Application.Models;
using DocumentChatbot.Core.Domain;

namespace DocumentChatbot.Web.Contracts;

public sealed record AskQuestionRequest(string Question);

public sealed record CitationResponse(
    string ChunkId,
    string DocumentId,
    string DocumentName,
    string? Chapter,
    int? PageNumber,
    int? SlideNumber,
    string Excerpt,
    double RelevanceScore)
{
    public static CitationResponse From(Citation citation) => new(
        citation.ChunkId,
        citation.DocumentId,
        citation.DocumentName,
        citation.Chapter,
        citation.PageNumber,
        citation.SlideNumber,
        citation.Excerpt,
        citation.RelevanceScore);
}

public sealed record ChatMessageResponse(
    Guid Id,
    string Role,
    string Content,
    DateTimeOffset SentAtUtc,
    IReadOnlyList<CitationResponse> Citations)
{
    public static ChatMessageResponse From(ChatMessage message) => new(
        message.Id,
        message.Role.ToString().ToLowerInvariant(),
        message.Content,
        message.SentAtUtc,
        message.Citations.Select(CitationResponse.From).ToArray());
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
    ChatMessageResponse UserMessage,
    ChatMessageResponse AssistantMessage,
    bool IsGrounded)
{
    public static AskQuestionResponse From(AskQuestionResult result) => new(
        result.SessionId,
        ChatMessageResponse.From(result.UserMessage),
        ChatMessageResponse.From(result.AssistantMessage),
        result.IsGrounded);
}
