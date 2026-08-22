using DocumentChatbot.Web.Services;
using DocumentChatbot.Web.Exceptions;
using DocumentChatbot.Web.Models;
using DocumentChatbot.Web.Authorization;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace DocumentChatbot.Web.Pages.Assignment2.Documents;

[Authorize(Policy = AppPolicies.SubjectLeaderOnly)]
public sealed class IndexModel(
    IDocumentService documentService,
    ICourseService courseService,
    IUserContext userContext) : PageModel
{
    private const int DemoCourseId = 1;

    public CourseSummary Course { get; private set; } = null!;
    public IReadOnlyList<DocumentSummary> Documents { get; private set; } = [];

    public async Task<IActionResult> OnGetAsync(CancellationToken cancellationToken)
    {
        var course = await GetManagedCourseAsync(DemoCourseId, cancellationToken);
        if (course is null)
        {
            return NotFound();
        }

        Course = course;
        Documents = await documentService.GetAllAsync(DemoCourseId, cancellationToken);
        return Page();
    }

    public async Task<IActionResult> OnPostDeleteAsync(Guid id, CancellationToken cancellationToken)
    {
        try
        {
            var document = await documentService.GetByIdAsync(id, cancellationToken);
            if (await GetManagedCourseAsync(document.CourseId, cancellationToken) is null)
            {
                return NotFound();
            }

            await documentService.DeleteAsync(id, cancellationToken);
            TempData["Success"] = "Đã xóa tài liệu.";
            return RedirectToPage();
        }
        catch (DocumentNotFoundException)
        {
            return NotFound();
        }
    }

    private Task<CourseSummary?> GetManagedCourseAsync(
        int courseId,
        CancellationToken cancellationToken) =>
        courseService.GetManagedCourseAsync(userContext.UserId, courseId, cancellationToken);
}
