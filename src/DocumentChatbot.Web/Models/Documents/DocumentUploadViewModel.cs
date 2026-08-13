using System.ComponentModel.DataAnnotations;

namespace DocumentChatbot.Web.Models;

public sealed class DocumentUploadViewModel
{
    [Range(1, int.MaxValue)]
    public int CourseId { get; set; }

    [Required, StringLength(255)]
    [Display(Name = "Tiêu đề tài liệu")]
    public string Title { get; set; } = string.Empty;

    [StringLength(255)]
    [Display(Name = "Chương (không bắt buộc)")]
    public string? Chapter { get; set; }

    [Required]
    [Display(Name = "Tệp PDF, DOCX hoặc PPTX")]
    public IFormFile File { get; set; } = null!;
}
