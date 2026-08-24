using DocumentChatbot.Web.Authorization;
using DocumentChatbot.Data;
using DocumentChatbot.Web.Models;
using DocumentChatbot.Web.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;

namespace DocumentChatbot.Web.Pages.Assignment2.Reports;

[Authorize(Policy = AppPolicies.SubjectLeaderOnly)]
public sealed class IndexModel(
    IDocumentService documentService,
    ICourseService courseService,
    IUserContext userContext,
    DocumentChatbotDbContext dbContext) : PageModel
{
    private const int DemoCourseId = 1;

    public CourseSummary Course { get; private set; } = null!;
    public ReportSummary Summary { get; private set; } = null!;
    public IReadOnlyList<BreakdownItem> TypeBreakdown { get; private set; } = [];
    public IReadOnlyList<BreakdownItem> ChapterBreakdown { get; private set; } = [];
    public IReadOnlyList<DocumentSummary> RecentDocuments { get; private set; } = [];
    [BindProperty(SupportsGet = true)] public DateTime From { get; set; }
    [BindProperty(SupportsGet = true)] public DateTime To { get; set; }
    public UsageSummary Usage { get; private set; } = null!;
    public IReadOnlyList<DailyUsagePoint> DailyUsage { get; private set; } = [];

    public async Task<IActionResult> OnGetAsync(CancellationToken cancellationToken)
    {
        var today = DateTime.UtcNow.Date;
        From = From == default ? new DateTime(today.Year, today.Month, 1) : From.Date;
        To = To == default ? today : To.Date;
        if (From > To)
        {
            (From, To) = (To, From);
        }

        var course = await courseService.GetManagedCourseAsync(
            userContext.UserId,
            DemoCourseId,
            cancellationToken);

        if (course is null)
        {
            return NotFound();
        }

        Course = course;

        var documents = await documentService.GetAllAsync(DemoCourseId, cancellationToken);
        Summary = new ReportSummary(
            documents.Count,
            documents.Count(document => document.Status == DocumentStatus.Indexed),
            documents.Count(document => document.Status == DocumentStatus.Processing),
            documents.Count(document => document.Status == DocumentStatus.Failed),
            documents.Sum(document => document.ChunkCount),
            documents.Sum(document => document.FileSizeBytes));

        TypeBreakdown = documents
            .GroupBy(document => document.FileType)
            .OrderByDescending(group => group.Count())
            .Select(group => new BreakdownItem(group.Key.ToString(), group.Count()))
            .ToArray();

        ChapterBreakdown = documents
            .GroupBy(document => string.IsNullOrWhiteSpace(document.Chapter) ? "Chưa phân chương" : document.Chapter)
            .OrderByDescending(group => group.Count())
            .ThenBy(group => group.Key)
            .Select(group => new BreakdownItem(group.Key!, group.Count()))
            .ToArray();

        RecentDocuments = documents.Take(5).ToArray();

        var endExclusive = To.AddDays(1);
        var messages = await dbContext.ChatMessages
            .AsNoTracking()
            .Where(message => message.ChatSession.CourseId == DemoCourseId &&
                              message.SentAtUtc >= From && message.SentAtUtc < endExclusive)
            .Select(message => new { message.Role, message.SentAtUtc })
            .ToArrayAsync(cancellationToken);

        Usage = new UsageSummary(
            messages.Count(message => message.Role == nameof(MessageRole.User)));

        DailyUsage = Enumerable.Range(0, (To - From).Days + 1)
            .Select(offset => From.AddDays(offset))
            .Select(date => new DailyUsagePoint(
                date,
                messages.Count(message => message.Role == nameof(MessageRole.User) && message.SentAtUtc.Date == date)))
            .ToArray();
        return Page();
    }

    public sealed record ReportSummary(
        int TotalDocuments,
        int IndexedDocuments,
        int ProcessingDocuments,
        int FailedDocuments,
        int TotalChunks,
        long TotalFileSizeBytes);

    public sealed record BreakdownItem(string Label, int Count);
    public sealed record UsageSummary(int QuestionCount);
    public sealed record DailyUsagePoint(DateTime Date, int Questions);
}
