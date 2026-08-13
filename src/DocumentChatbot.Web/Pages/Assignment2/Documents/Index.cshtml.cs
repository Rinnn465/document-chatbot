using DocumentChatbot.Web.Services;
using DocumentChatbot.Web.Exceptions;
using DocumentChatbot.Web.Models;
using DocumentChatbot.Web.Authorization;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace DocumentChatbot.Web.Pages.Assignment2.Documents;

[Authorize(Policy = AppPolicies.SubjectLeaderOnly)]
public sealed class IndexModel(IDocumentService documentService) : PageModel
{
    public IReadOnlyList<DocumentSummary> Documents { get; private set; } = [];

    public async Task OnGetAsync(CancellationToken cancellationToken) =>
        Documents = await documentService.GetAllAsync(1, cancellationToken);

    public async Task<IActionResult> OnPostDeleteAsync(Guid id, CancellationToken cancellationToken)
    {
        try
        {
            await documentService.DeleteAsync(id, cancellationToken);
            TempData["Success"] = "Đã xóa tài liệu.";
            return RedirectToPage();
        }
        catch (DocumentNotFoundException)
        {
            return NotFound();
        }
    }
}
