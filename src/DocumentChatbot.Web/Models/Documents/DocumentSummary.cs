namespace DocumentChatbot.Web.Models;

/// <summary>
/// Metadata about an uploaded document. The document's chunks/embeddings
/// live in the RAG service's vector store, not here — this is only the
/// bookkeeping needed to list, show and delete documents from the UI.
/// </summary>
public sealed class DocumentSummary
{
    public DocumentSummary(
        Guid id,
        int courseId,
        string title,
        string originalFileName,
        DocumentType fileType,
        long fileSizeBytes,
        string? chapter,
        Guid uploadedById,
        string uploadedByName,
        DateTimeOffset uploadedAtUtc,
        DocumentStatus status = DocumentStatus.Processing,
        int chunkCount = 0,
        string? processingError = null)
    {
        Id = id;
        CourseId = courseId;
        Title = title.Trim();
        OriginalFileName = originalFileName.Trim();
        FileType = fileType;
        FileSizeBytes = fileSizeBytes;
        Chapter = string.IsNullOrWhiteSpace(chapter) ? null : chapter.Trim();
        UploadedById = uploadedById;
        UploadedByName = uploadedByName.Trim();
        UploadedAtUtc = uploadedAtUtc;
        Status = status;
        ChunkCount = chunkCount;
        ProcessingError = processingError;
    }

    public Guid Id { get; }
    public int CourseId { get; }
    public string Title { get; }
    public string OriginalFileName { get; }
    public DocumentType FileType { get; }
    public long FileSizeBytes { get; }
    public string? Chapter { get; }
    public Guid UploadedById { get; }
    public string UploadedByName { get; }
    public DateTimeOffset UploadedAtUtc { get; }

    public DocumentStatus Status { get; private set; }
    public string? ProcessingError { get; private set; }
    public int ChunkCount { get; private set; }

    public void MarkIndexed(int chunkCount)
    {
        ChunkCount = chunkCount;
        Status = chunkCount > 0 ? DocumentStatus.Indexed : DocumentStatus.Failed;
        ProcessingError = chunkCount > 0 ? null : "No extractable text found in document.";
    }

    public void MarkFailed(string error)
    {
        Status = DocumentStatus.Failed;
        ProcessingError = error;
    }
}
