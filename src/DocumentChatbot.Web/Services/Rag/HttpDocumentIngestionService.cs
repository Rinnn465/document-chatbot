using System.Net.Http.Json;
using DocumentChatbot.Web.Services;
using DocumentChatbot.Web.Models;

namespace DocumentChatbot.Web.Services;

public sealed class HttpDocumentIngestionService(HttpClient httpClient) : IDocumentIngestionService
{
    public async Task<DocumentIngestResult> IngestAsync(
        string documentId,
        string documentName,
        string? chapter,
        IReadOnlyList<ExtractedDocumentSection> sections,
        CancellationToken cancellationToken = default)
    {
        var request = new IngestRequest(documentId, documentName, chapter, sections);

        using var response = await httpClient.PostAsJsonAsync("/documents", request, cancellationToken);
        response.EnsureSuccessStatusCode();

        var payload = await response.Content.ReadFromJsonAsync<IngestResponse>(cancellationToken: cancellationToken)
            ?? throw new InvalidOperationException("RAG service returned an empty ingest response.");

        return new DocumentIngestResult(payload.ChunkCount);
    }

    public async Task DeleteAsync(string documentId, CancellationToken cancellationToken = default)
    {
        using var response = await httpClient.DeleteAsync($"/documents/{documentId}", cancellationToken);
        response.EnsureSuccessStatusCode();
    }

    public async Task<DocumentChunkPage> GetChunksAsync(
        string documentId,
        int page,
        int pageSize,
        CancellationToken cancellationToken = default)
    {
        using var response = await httpClient.GetAsync(
            $"/documents/{Uri.EscapeDataString(documentId)}/chunks?page={page}&pageSize={pageSize}",
            cancellationToken);
        response.EnsureSuccessStatusCode();

        return await response.Content.ReadFromJsonAsync<DocumentChunkPage>(
                   cancellationToken: cancellationToken)
               ?? throw new InvalidOperationException("RAG service returned an empty chunk response.");
    }

    private sealed record IngestRequest(
        string DocumentId,
        string DocumentName,
        string? Chapter,
        IReadOnlyList<ExtractedDocumentSection> Sections);

    private sealed class IngestResponse
    {
        public string? DocumentId { get; init; }
        public int ChunkCount { get; init; }
    }
}
