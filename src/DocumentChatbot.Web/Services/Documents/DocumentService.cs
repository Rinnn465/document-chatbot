using DocumentChatbot.Web.Exceptions;
using DocumentChatbot.Web.Models;

namespace DocumentChatbot.Web.Services;

public sealed class DocumentService(
    IDocumentRepository documents,
    ITextExtractor textExtractor,
    IDocumentIngestionService ingestionService,
    IDocumentProcessingQueue processingQueue,
    IDocumentStatusNotifier statusNotifier,
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
        var document = CreateDocument(
            courseId, title, chapter, originalFileName, fileType,
            fileSizeBytes, uploadedById, uploadedByName);
        await documents.AddAsync(document, cancellationToken);
        await statusNotifier.NotifyAsync(
            document,
            "uploaded",
            10,
            "Server đã nhận tệp và tạo bản ghi tài liệu.",
            cancellationToken);

        return await ProcessDocumentAsync(document, fileType, fileStream, cancellationToken);
    }

    public async Task<DocumentSummary> QueueUploadAsync(
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
        await using var buffer = new MemoryStream();
        await fileStream.CopyToAsync(buffer, cancellationToken);

        var document = CreateDocument(
            courseId, title, chapter, originalFileName, fileType,
            fileSizeBytes, uploadedById, uploadedByName);
        await documents.AddAsync(document, cancellationToken);
        await statusNotifier.NotifyAsync(
            document,
            "queued",
            10,
            "Đã upload tệp; tài liệu đang chờ BackgroundService xử lý.",
            cancellationToken);

        await processingQueue.QueueAsync(
            new DocumentProcessingJob(document.Id, fileType, buffer.ToArray()),
            cancellationToken);
        return document;
    }

    public async Task ProcessQueuedUploadAsync(
        DocumentProcessingJob job,
        CancellationToken cancellationToken = default)
    {
        var document = await GetByIdAsync(job.DocumentId, cancellationToken);
        await using var stream = new MemoryStream(job.FileContent, writable: false);
        await ProcessDocumentAsync(document, job.FileType, stream, cancellationToken);
    }

    private async Task<DocumentSummary> ProcessDocumentAsync(
        DocumentSummary document,
        DocumentType fileType,
        Stream fileStream,
        CancellationToken cancellationToken)
    {

        try
        {
            await statusNotifier.NotifyAsync(
                document,
                "extracting",
                30,
                "C# đang trích xuất nội dung theo trang, slide hoặc đoạn văn.",
                cancellationToken);

            var extractedDocument = textExtractor.Extract(fileStream, fileType);
            if (!extractedDocument.HasContent)
            {
                throw new InvalidOperationException("No extractable text found in document.");
            }

            await statusNotifier.NotifyAsync(
                document,
                "indexing",
                65,
                $"Đã trích xuất {extractedDocument.Sections.Count} phần; Python đang chunk, tạo embedding và lưu Chroma.",
                cancellationToken);

            var result = await ingestionService.IngestAsync(
                document.Id.ToString("N"),
                document.Title,
                document.Chapter,
                extractedDocument.Sections,
                cancellationToken);

            document.MarkIndexed(result.ChunkCount);
            await documents.SaveAsync(document, cancellationToken);
            await statusNotifier.NotifyAsync(
                document,
                "indexed",
                100,
                $"Lập chỉ mục hoàn tất: {document.ChunkCount} chunks đã sẵn sàng.",
                cancellationToken);
        }
        catch (Exception ex)
        {
            document.MarkFailed(ex.Message);
            await documents.SaveAsync(document, CancellationToken.None);
            await statusNotifier.NotifyAsync(
                document,
                "failed",
                100,
                $"Lập chỉ mục thất bại: {ex.Message}",
                CancellationToken.None);
        }

        return document;
    }

    private DocumentSummary CreateDocument(
        int courseId,
        string title,
        string? chapter,
        string originalFileName,
        DocumentType fileType,
        long fileSizeBytes,
        Guid uploadedById,
        string uploadedByName) => new(
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

    public async Task<DocumentSummary> GetByIdAsync(Guid id, CancellationToken cancellationToken = default) =>
        await documents.GetByIdAsync(id, cancellationToken)
            ?? throw new DocumentNotFoundException(id);

    public Task<IReadOnlyList<DocumentSummary>> GetAllAsync(
        int courseId,
        CancellationToken cancellationToken = default) =>
        documents.GetAllAsync(courseId, cancellationToken);

    public async Task<DocumentChunkPage> GetChunksAsync(
        Guid id,
        int page,
        int pageSize,
        CancellationToken cancellationToken = default)
    {
        var document = await GetByIdAsync(id, cancellationToken);
        if (document.Status != DocumentStatus.Indexed)
        {
            throw new InvalidOperationException("Only indexed documents have chunks to display.");
        }

        return await ingestionService.GetChunksAsync(
            document.Id.ToString("N"),
            page,
            pageSize,
            cancellationToken);
    }

    public async Task DeleteAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var document = await GetByIdAsync(id, cancellationToken);
        await ingestionService.DeleteAsync(document.Id.ToString("N"), cancellationToken);
        await documents.DeleteAsync(id, cancellationToken);
    }
}
