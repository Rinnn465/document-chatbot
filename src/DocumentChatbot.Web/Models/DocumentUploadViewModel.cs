using System.ComponentModel.DataAnnotations;

namespace DocumentChatbot.Web.Models;

public sealed class DocumentUploadViewModel
{
    [Required, StringLength(255)]
    [Display(Name = "Document title")]
    public string Title { get; set; } = string.Empty;

    [StringLength(255)]
    [Display(Name = "Chapter (optional)")]
    public string? Chapter { get; set; }

    [Required]
    [Display(Name = "File (PDF, DOCX, PPTX)")]
    public IFormFile File { get; set; } = null!;
}
