using System.ComponentModel.DataAnnotations;
using DocumentChatbot.Web.Services;
using DocumentChatbot.Web.Models;
using DocumentChatbot.Web.Authorization;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace DocumentChatbot.Web.Pages.Assignment2.Documents;

[Authorize(Policy = AppPolicies.SubjectLeaderOnly)]
public sealed class UploadModel(
    IDocumentService documentService,
    IUserContext userContext) : PageModel
{
    private static readonly Dictionary<string, DocumentType> AllowedExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        [".pdf"] = DocumentType.Pdf,
        [".docx"] = DocumentType.Docx,
        [".pptx"] = DocumentType.Slide
    };

    private const long MaxFileSizeBytes = 25 * 1024 * 1024;

    [BindProperty]
    public InputModel Input { get; set; } = new();

    public void OnGet() { }

    public async Task<IActionResult> OnPostAsync(CancellationToken cancellationToken)
    {
        if (!AllowedExtensions.TryGetValue(Path.GetExtension(Input.File?.FileName ?? string.Empty), out var fileType))
        {
            ModelState.AddModelError("Input.File", "Chỉ chấp nhận tệp .pdf, .docx và .pptx.");
        }
        else if (Input.File!.Length == 0)
        {
            ModelState.AddModelError("Input.File", "Tệp được chọn không có nội dung.");
        }
        else if (Input.File.Length > MaxFileSizeBytes)
        {
            ModelState.AddModelError("Input.File", "Tệp vượt quá giới hạn 25 MB.");
        }

        if (!ModelState.IsValid) return Page();

        await using var stream = Input.File!.OpenReadStream();
        var document = await documentService.UploadAsync(
            1,
            Input.Title, Input.Chapter, Input.File.FileName, fileType, stream,
            Input.File.Length, userContext.UserId, userContext.DisplayName, cancellationToken);

        if (document.Status == DocumentStatus.Indexed)
            TempData["Success"] = $"Đã upload và lập chỉ mục tài liệu \"{document.Title}\".";
        else
            TempData["Error"] = $"Đã nhận tài liệu nhưng lập chỉ mục thất bại: {document.ProcessingError}";

        return RedirectToPage("/Assignment2/Documents/Details", new { id = document.Id });
    }

    public sealed class InputModel
    {
        [Required, StringLength(255), Display(Name = "Tiêu đề tài liệu")]
        public string Title { get; set; } = string.Empty;

        [StringLength(255), Display(Name = "Chương (không bắt buộc)")]
        public string? Chapter { get; set; }

        [Required, Display(Name = "Tệp PDF, DOCX hoặc PPTX")]
        public IFormFile File { get; set; } = null!;
    }
}
