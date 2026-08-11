using System.Net.Http.Json;
using DocumentChatbot.Core.Application.Abstractions;
using DocumentChatbot.Core.Application.Models;

namespace DocumentChatbot.Infrastructure.Rag;

public sealed class HttpDocumentIngestionService(HttpClient httpClient) : IDocumentIngestionService
{
    public async Task<DocumentIngestResult> IngestAsync(
        string documentId,
        string documentName,
        string? chapter,
        string text,
        CancellationToken cancellationToken = default)
    {
        var request = new IngestRequest(documentId, documentName, chapter, text);

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

    private sealed record IngestRequest(string DocumentId, string DocumentName, string? Chapter, string Text);

    private sealed class IngestResponse
    {
        public string? DocumentId { get; init; }
        public int ChunkCount { get; init; }
    }
}
