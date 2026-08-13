using DocumentChatbot.Data;
using DocumentChatbot.Web.Models;
using Microsoft.EntityFrameworkCore;

namespace DocumentChatbot.Web.Services;

public sealed class SqlDocumentRepository(DocumentChatbotDbContext dbContext)
    : IDocumentRepository
{
    public async Task AddAsync(
        DocumentSummary document,
        CancellationToken cancellationToken = default)
    {
        dbContext.Documents.Add(new DocumentEntity
        {
            DocumentId = document.Id,
            CourseId = document.CourseId,
            Title = document.Title,
            Chapter = document.Chapter,
            OriginalFileName = document.OriginalFileName,
            FileType = document.FileType.ToString(),
            FileSizeBytes = document.FileSizeBytes,
            Status = document.Status.ToString(),
            ChunkCount = document.ChunkCount,
            ProcessingError = document.ProcessingError,
            UploadedByUserId = document.UploadedById,
            UploadedAtUtc = document.UploadedAtUtc.UtcDateTime
        });

        await dbContext.SaveChangesAsync(cancellationToken);
    }

    public async Task<DocumentSummary?> GetByIdAsync(
        Guid id,
        CancellationToken cancellationToken = default)
    {
        var entity = await DocumentQuery()
            .SingleOrDefaultAsync(document => document.DocumentId == id, cancellationToken);

        return entity is null ? null : Map(entity);
    }

    public async Task<IReadOnlyList<DocumentSummary>> GetAllAsync(
        int courseId,
        CancellationToken cancellationToken = default)
    {
        var entities = await DocumentQuery()
            .Where(document => document.CourseId == courseId)
            .OrderByDescending(document => document.UploadedAtUtc)
            .ToArrayAsync(cancellationToken);

        return entities.Select(Map).ToArray();
    }

    public async Task SaveAsync(
        DocumentSummary document,
        CancellationToken cancellationToken = default)
    {
        var entity = await dbContext.Documents
            .SingleAsync(item => item.DocumentId == document.Id, cancellationToken);

        entity.Title = document.Title;
        entity.Chapter = document.Chapter;
        entity.Status = document.Status.ToString();
        entity.ChunkCount = document.ChunkCount;
        entity.ProcessingError = document.ProcessingError;

        await dbContext.SaveChangesAsync(cancellationToken);
    }

    public async Task DeleteAsync(
        Guid id,
        CancellationToken cancellationToken = default)
    {
        var entity = await dbContext.Documents
            .SingleOrDefaultAsync(document => document.DocumentId == id, cancellationToken);

        if (entity is null)
        {
            return;
        }

        dbContext.Documents.Remove(entity);
        await dbContext.SaveChangesAsync(cancellationToken);
    }

    private IQueryable<DocumentEntity> DocumentQuery() =>
        dbContext.Documents
            .AsNoTracking()
            .Include(document => document.UploadedByUser);

    private static DocumentSummary Map(DocumentEntity entity)
    {
        var document = new DocumentSummary(
            entity.DocumentId,
            entity.CourseId,
            entity.Title,
            entity.OriginalFileName,
            Enum.Parse<DocumentType>(entity.FileType, ignoreCase: true),
            entity.FileSizeBytes,
            entity.Chapter,
            entity.UploadedByUserId,
            entity.UploadedByUser.DisplayName,
            AsUtc(entity.UploadedAtUtc));

        if (Enum.TryParse<DocumentStatus>(entity.Status, ignoreCase: true, out var status))
        {
            if (status == DocumentStatus.Indexed)
            {
                document.MarkIndexed(entity.ChunkCount);
            }
            else if (status == DocumentStatus.Failed)
            {
                document.MarkFailed(entity.ProcessingError ?? "Document processing failed.");
            }
        }

        return document;
    }

    private static DateTimeOffset AsUtc(DateTime value) =>
        new(DateTime.SpecifyKind(value, DateTimeKind.Utc));
}
