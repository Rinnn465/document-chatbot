using DocumentChatbot.Web.Exceptions;
using DocumentChatbot.Web.Models;

namespace DocumentChatbot.Web.Services;

public sealed class DocumentService(
    IDocumentRepository documents,
    ITextExtractor textExtractor,
    IDocumentIngestionService ingestionService,
    TimeProvider timeProvider) : IDocumentService
{
    public async Task<DocumentSummary> UploadAsync(
        int courseId,
        string title,
        string? chapter,
        string originalFileName,
        DocumentType fileType,
        Stream fileStream,
        long fileSizeBytes,
        Guid uploadedById,
        string uploadedByName,
        CancellationToken cancellationToken = default)
    {
        var document = new DocumentSummary(
            Guid.NewGuid(),
            courseId,
            title,
            originalFileName,
            fileType,
            fileSizeBytes,
            chapter,
            uploadedById,
            uploadedByName,
            timeProvider.GetUtcNow());

        await documents.AddAsync(document, cancellationToken);

        try
        {
            var extractedDocument = textExtractor.Extract(fileStream, fileType);
            if (!extractedDocument.HasContent)
            {
                throw new InvalidOperationException("No extractable text found in document.");
            }

            var result = await ingestionService.IngestAsync(
                document.Id.ToString("N"),
                document.Title,
                document.Chapter,
                extractedDocument.Sections,
                cancellationToken);

            document.MarkIndexed(result.ChunkCount);
        }
        catch (Exception ex)
        {
            document.MarkFailed(ex.Message);
        }

        await documents.SaveAsync(document, cancellationToken);
        return document;
    }

    public async Task<DocumentSummary> GetByIdAsync(Guid id, CancellationToken cancellationToken = default) =>
        await documents.GetByIdAsync(id, cancellationToken)
            ?? throw new DocumentNotFoundException(id);

    public Task<IReadOnlyList<DocumentSummary>> GetAllAsync(
        int courseId,
        CancellationToken cancellationToken = default) =>
        documents.GetAllAsync(courseId, cancellationToken);

    public async Task DeleteAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var document = await GetByIdAsync(id, cancellationToken);
        await ingestionService.DeleteAsync(document.Id.ToString("N"), cancellationToken);
        await documents.DeleteAsync(id, cancellationToken);
    }
}
