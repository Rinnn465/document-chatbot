namespace DocumentChatbot.Core.Domain;

public sealed record Citation(
    string ChunkId,
    string DocumentId,
    string DocumentName,
    string? Chapter,
    int? PageNumber,
    int? SlideNumber,
    string Excerpt,
    double RelevanceScore)
{
    public bool IsValid =>
        !string.IsNullOrWhiteSpace(ChunkId) &&
        !string.IsNullOrWhiteSpace(DocumentId) &&
        !string.IsNullOrWhiteSpace(DocumentName);
}
