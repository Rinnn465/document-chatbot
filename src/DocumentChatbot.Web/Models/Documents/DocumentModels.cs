namespace DocumentChatbot.Web.Models;

public sealed record DocumentIngestResult(int ChunkCount);

public sealed record DocumentChunk(
    string ChunkId,
    int ChunkIndex,
    string SectionType,
    int SectionIndex,
    int? SectionNumber,
    int SectionChunkIndex,
    string? SectionTitle,
    string ContentHash,
    string Content);

public sealed record DocumentChunkPage(
    string DocumentId,
    string DocumentName,
    string? Chapter,
    int TotalCount,
    int Page,
    int PageSize,
    IReadOnlyList<DocumentChunk> Items)
{
    public int TotalPages => Math.Max(1, (int)Math.Ceiling(TotalCount / (double)PageSize));
}

public sealed record DocumentChunksViewModel(
    CourseSummary Course,
    DocumentSummary Document,
    DocumentChunkPage Chunks);

/// <summary>
/// Text extracted from one logical part of a document. Keeping the original
/// page/slide boundary prevents unrelated presentation slides from being
/// merged into the same vector chunk.
/// </summary>
public sealed record ExtractedDocumentSection(
    string SectionType,
    int? SectionNumber,
    string? Title,
    string Content);

public sealed record ExtractedDocument(IReadOnlyList<ExtractedDocumentSection> Sections)
{
    public bool HasContent => Sections.Any(section => !string.IsNullOrWhiteSpace(section.Content));
}
