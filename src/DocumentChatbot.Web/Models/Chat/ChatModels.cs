using DocumentChatbot.Web.Models;

namespace DocumentChatbot.Web.Models;

public sealed record ChatHistoryItem(MessageRole Role, string Content);

public sealed record RagQuestion(
    string Question,
    IReadOnlyList<ChatHistoryItem> ConversationHistory);

public sealed record RagAnswer(
    string Answer,
    bool IsGrounded,
    IReadOnlyList<Citation> Citations,
    int InputTokens = 0,
    int OutputTokens = 0,
    int TotalTokens = 0);

public sealed record AskQuestionResult(
    Guid SessionId,
    string SessionTitle,
    ChatMessage UserMessage,
    ChatMessage AssistantMessage,
    bool IsGrounded);
