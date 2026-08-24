using System.Net.Http.Json;
using DocumentChatbot.Web.Models;
using Microsoft.Extensions.Options;

namespace DocumentChatbot.Web.Services;

public sealed class HttpRagService(
    HttpClient httpClient,
    IOptions<RagServiceOptions> options) : IRagService
{
    public async Task<RagAnswer> AnswerAsync(
        RagQuestion question,
        CancellationToken cancellationToken = default)
    {
        var settings = options.Value;
        var request = new RagRequest(
            question.Question,
            question.ConversationHistory
                .Select(item => new RagHistoryItem(item.Role.ToString().ToLowerInvariant(), item.Content))
                .ToArray());

        using var response = await httpClient.PostAsJsonAsync(
            settings.AskPath,
            request,
            cancellationToken);
        response.EnsureSuccessStatusCode();

        var payload = await response.Content.ReadFromJsonAsync<RagResponse>(
            cancellationToken: cancellationToken)
            ?? throw new InvalidOperationException("RAG service returned an empty response.");

        var citations = payload.Sources
            .Select((source, index) => MapCitation(source, index))
            .Where(citation => citation.IsValid)
            .ToArray();

        var isGrounded = payload.Grounded ?? citations.Length > 0;
        return new RagAnswer(payload.Answer ?? string.Empty, isGrounded, citations,
            payload.Usage?.InputTokens ?? 0, payload.Usage?.OutputTokens ?? 0, payload.Usage?.TotalTokens ?? 0);
    }

    private static Citation MapCitation(RagSource source, int index)
    {
        var documentName = FirstNotBlank(source.DocumentName, source.Source) ?? string.Empty;
        var documentId = FirstNotBlank(source.DocumentId, documentName) ?? string.Empty;
        var fallbackChunkId = string.IsNullOrWhiteSpace(documentId)
            ? string.Empty
            : $"{documentId}:{index + 1}";
        var chunkId = FirstNotBlank(source.ChunkId, fallbackChunkId) ?? string.Empty;

        return new Citation(
            chunkId,
            source.ChunkIndex,
            documentId,
            documentName,
            source.Chapter,
            source.PageNumber,
            source.SlideNumber,
            source.Excerpt ?? string.Empty,
            source.RelevanceScore ?? 0);
    }

    private static string? FirstNotBlank(params string?[] values) =>
        values.FirstOrDefault(value => !string.IsNullOrWhiteSpace(value));

    private sealed record RagRequest(
        string Question,
        IReadOnlyList<RagHistoryItem> ConversationHistory);

    private sealed record RagHistoryItem(string Role, string Content);

    private sealed class RagResponse
    {
        public string? Answer { get; init; }
        public bool? Grounded { get; init; }
        public List<RagSource> Sources { get; init; } = [];
        public RagUsage? Usage { get; init; }
    }

    private sealed class RagUsage
    {
        public int InputTokens { get; init; }
        public int OutputTokens { get; init; }
        public int TotalTokens { get; init; }
    }

    private sealed class RagSource
    {
        public string? Source { get; init; }
        public string? ChunkId { get; init; }
        public int? ChunkIndex { get; init; }
        public string? DocumentId { get; init; }
        public string? DocumentName { get; init; }
        public string? Chapter { get; init; }
        public int? PageNumber { get; init; }
        public int? SlideNumber { get; init; }
        public string? Excerpt { get; init; }
        public double? RelevanceScore { get; init; }
    }
}
