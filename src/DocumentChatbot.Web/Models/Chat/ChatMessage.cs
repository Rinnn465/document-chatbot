namespace DocumentChatbot.Web.Models;

public sealed class ChatMessage
{
    public ChatMessage(
        Guid id,
        MessageRole role,
        string content,
        DateTimeOffset sentAtUtc,
        IReadOnlyCollection<Citation>? citations = null,
        int inputTokens = 0,
        int outputTokens = 0,
        int totalTokens = 0)
    {
        if (id == Guid.Empty)
        {
            throw new ArgumentException("Message id is required.", nameof(id));
        }

        if (string.IsNullOrWhiteSpace(content))
        {
            throw new ArgumentException("Message content is required.", nameof(content));
        }

        Id = id;
        Role = role;
        Content = content.Trim();
        SentAtUtc = sentAtUtc;
        InputTokens = inputTokens;
        OutputTokens = outputTokens;
        TotalTokens = totalTokens;
        Citations = citations?.ToArray() ?? [];
    }

    public Guid Id { get; }
    public MessageRole Role { get; }
    public string Content { get; }
    public DateTimeOffset SentAtUtc { get; }
    public int InputTokens { get; }
    public int OutputTokens { get; }
    public int TotalTokens { get; }
    public IReadOnlyList<Citation> Citations { get; }
}
