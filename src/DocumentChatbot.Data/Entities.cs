namespace DocumentChatbot.Data;

public sealed class RoleEntity
{
    public byte RoleId { get; set; }
    public string Name { get; set; } = string.Empty;
}

public sealed class UserEntity
{
    public Guid UserId { get; set; }
    public string Email { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;
    public string? PasswordHash { get; set; }
    public byte RoleId { get; set; }
    public bool IsActive { get; set; }
    public DateTime CreatedAtUtc { get; set; }
    public RoleEntity Role { get; set; } = null!;
}

public sealed class CourseEntity
{
    public int CourseId { get; set; }
    public string Code { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public Guid SubjectLeaderId { get; set; }
    public bool IsActive { get; set; }
    public DateTime CreatedAtUtc { get; set; }
}

public sealed class CourseEnrollmentEntity
{
    public int CourseId { get; set; }
    public Guid StudentId { get; set; }
    public DateTime EnrolledAtUtc { get; set; }
}

public sealed class DocumentEntity
{
    public Guid DocumentId { get; set; }
    public int CourseId { get; set; }
    public string Title { get; set; } = string.Empty;
    public string? Chapter { get; set; }
    public string OriginalFileName { get; set; } = string.Empty;
    public string FileType { get; set; } = string.Empty;
    public long FileSizeBytes { get; set; }
    public string Status { get; set; } = string.Empty;
    public int ChunkCount { get; set; }
    public string? ProcessingError { get; set; }
    public Guid UploadedByUserId { get; set; }
    public DateTime UploadedAtUtc { get; set; }
    public UserEntity UploadedByUser { get; set; } = null!;
}

public sealed class ChatSessionEntity
{
    public Guid ChatSessionId { get; set; }
    public int CourseId { get; set; }
    public Guid UserId { get; set; }
    public string Title { get; set; } = string.Empty;
    public DateTime CreatedAtUtc { get; set; }
    public List<ChatMessageEntity> Messages { get; set; } = [];
}

public sealed class ChatMessageEntity
{
    public Guid ChatMessageId { get; set; }
    public Guid ChatSessionId { get; set; }
    public string Role { get; set; } = string.Empty;
    public string Content { get; set; } = string.Empty;
    public DateTime SentAtUtc { get; set; }
    public List<CitationEntity> Citations { get; set; } = [];
}

public sealed class CitationEntity
{
    public long CitationId { get; set; }
    public Guid ChatMessageId { get; set; }
    public Guid DocumentId { get; set; }
    public string ChunkId { get; set; } = string.Empty;
    public int? ChunkIndex { get; set; }
    public int? PageNumber { get; set; }
    public int? SlideNumber { get; set; }
    public string Excerpt { get; set; } = string.Empty;
    public decimal RelevanceScore { get; set; }
    public DocumentEntity Document { get; set; } = null!;
}
