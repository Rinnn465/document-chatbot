using DocumentChatbot.Web.Authorization;
using DocumentChatbot.Web.Exceptions;
using DocumentChatbot.Web.Models;
using DocumentChatbot.Web.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace DocumentChatbot.Web.Pages.Assignment2.Documents;

[Authorize(Policy = AppPolicies.SubjectLeaderOnly)]
public sealed class ChunksModel(
    IDocumentService documentService,
    ICourseService courseService,
    IUserContext userContext) : PageModel
{
    private const int PageSize = 10;

    public CourseSummary Course { get; private set; } = null!;
    public DocumentSummary Document { get; private set; } = null!;
    public DocumentChunkPage Chunks { get; private set; } = null!;

    public async Task<IActionResult> OnGetAsync(
        Guid id,
        int page = 1,
        CancellationToken cancellationToken = default)
    {
        if (page < 1)
        {
            return BadRequest();
        }

        try
        {
            var document = await documentService.GetByIdAsync(id, cancellationToken);
            var course = await courseService.GetManagedCourseAsync(
                userContext.UserId,
                document.CourseId,
                cancellationToken);
            if (course is null)
            {
                return NotFound();
            }

            var chunks = await documentService.GetChunksAsync(
                id,
                page,
                PageSize,
                cancellationToken);
            if (page > chunks.TotalPages)
            {
                return RedirectToPage(new { id, page = chunks.TotalPages });
            }

            Course = course;
            Document = document;
            Chunks = chunks;
            return Page();
        }
        catch (DocumentNotFoundException)
        {
            return NotFound();
        }
        catch (InvalidOperationException exception)
        {
            TempData["Error"] = exception.Message;
            return RedirectToPage("/Assignment2/Documents/Details", new { id });
        }
        catch (HttpRequestException)
        {
            TempData["Error"] = "Không thể tải danh sách chunks từ RAG service. Hãy kiểm tra kết nối và thử lại.";
            return RedirectToPage("/Assignment2/Documents/Details", new { id });
        }
    }
}
