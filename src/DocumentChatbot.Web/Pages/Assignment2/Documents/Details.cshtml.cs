using DocumentChatbot.Web.Services;
using DocumentChatbot.Web.Exceptions;
using DocumentChatbot.Web.Models;
using DocumentChatbot.Web.Authorization;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace DocumentChatbot.Web.Pages.Assignment2.Documents;

[Authorize(Policy = AppPolicies.SubjectLeaderOnly)]
public sealed class DetailsModel(IDocumentService documentService) : PageModel
{
    public DocumentSummary Document { get; private set; } = null!;

    public async Task<IActionResult> OnGetAsync(Guid id, CancellationToken cancellationToken)
    {
        try
        {
            Document = await documentService.GetByIdAsync(id, cancellationToken);
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
            await documentService.DeleteAsync(id, cancellationToken);
            TempData["Success"] = "Đã xóa tài liệu.";
            return RedirectToPage("/Assignment2/Documents/Index");
        }
        catch (DocumentNotFoundException)
        {
            return NotFound();
        }
    }
}
