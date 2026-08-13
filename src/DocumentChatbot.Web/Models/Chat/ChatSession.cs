namespace DocumentChatbot.Web.Models;

public sealed class ChatSession
{
    public const int MaximumTitleLength = 100;

    private readonly List<ChatMessage> _messages = [];
    private readonly object _messageLock = new();

    public ChatSession(
        Guid id,
        int courseId,
        Guid userId,
        string title,
        DateTimeOffset createdAtUtc)
    {
        if (id == Guid.Empty)
        {
            throw new ArgumentException("Session id is required.", nameof(id));
        }

        if (userId == Guid.Empty)
        {
            throw new ArgumentException("User id is required.", nameof(userId));
        }

        Id = id;
        CourseId = courseId;
        UserId = userId;
        Title = title.Trim();
        CreatedAtUtc = createdAtUtc;
        UpdatedAtUtc = createdAtUtc;
    }

    public Guid Id { get; }
    public int CourseId { get; }
    public Guid UserId { get; }
    public string Title { get; private set; }
    public DateTimeOffset CreatedAtUtc { get; }
    public DateTimeOffset UpdatedAtUtc { get; private set; }

    public IReadOnlyList<ChatMessage> Messages
    {
        get
        {
            lock (_messageLock)
            {
                return _messages.ToArray();
            }
        }
    }

    public void Append(ChatMessage message)
    {
        ArgumentNullException.ThrowIfNull(message);

        lock (_messageLock)
        {
            _messages.Add(message);
            UpdatedAtUtc = message.SentAtUtc;
        }
    }

    public void SetTitleFromFirstQuestion(string question)
    {
        if (string.IsNullOrWhiteSpace(question))
        {
            return;
        }

        const int maximumTitleLength = 55;
        var normalized = string.Join(' ', question.Split(
            [' ', '\r', '\n', '\t'],
            StringSplitOptions.RemoveEmptyEntries));

        lock (_messageLock)
        {
            if (_messages.Count > 0)
            {
                return;
            }

            Title = normalized.Length <= maximumTitleLength
                ? normalized
                : $"{normalized[..(maximumTitleLength - 1)].TrimEnd()}…";
        }
    }

    public void Rename(string title)
    {
        var normalized = string.Join(' ', (title ?? string.Empty).Split(
            [' ', '\r', '\n', '\t'],
            StringSplitOptions.RemoveEmptyEntries));

        if (normalized.Length == 0)
        {
            throw new ArgumentException("Session title is required.", nameof(title));
        }

        if (normalized.Length > MaximumTitleLength)
        {
            throw new ArgumentException(
                $"Session title cannot exceed {MaximumTitleLength} characters.",
                nameof(title));
        }

        lock (_messageLock)
        {
            Title = normalized;
        }
    }
}
