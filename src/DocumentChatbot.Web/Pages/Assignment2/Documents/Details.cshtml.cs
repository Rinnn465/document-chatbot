using DocumentChatbot.Web.Services;
using DocumentChatbot.Web.Exceptions;
using DocumentChatbot.Web.Models;
using DocumentChatbot.Web.Authorization;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace DocumentChatbot.Web.Pages.Assignment2.Documents;

[Authorize(Policy = AppPolicies.SubjectLeaderOnly)]
public sealed class DetailsModel(
    IDocumentService documentService,
    ICourseService courseService,
    IUserContext userContext) : PageModel
{
    public CourseSummary Course { get; private set; } = null!;
    public DocumentSummary Document { get; private set; } = null!;

    public async Task<IActionResult> OnGetAsync(Guid id, CancellationToken cancellationToken)
    {
        try
        {
            var document = await documentService.GetByIdAsync(id, cancellationToken);
            var course = await GetManagedCourseAsync(document.CourseId, cancellationToken);
            if (course is null)
            {
                return NotFound();
            }

            Course = course;
            Document = document;
            return Page();
        }
        catch (DocumentNotFoundException)
        {
            return NotFound();
        }
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
            return RedirectToPage("/Assignment2/Documents/Index");
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
