namespace DocumentChatbot.Web.Models;

public sealed record DocumentIngestResult(int ChunkCount);

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
