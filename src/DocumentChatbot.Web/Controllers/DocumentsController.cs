using DocumentChatbot.Web.Services;
using DocumentChatbot.Web.Exceptions;
using DocumentChatbot.Web.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using DocumentChatbot.Web.Authorization;

namespace DocumentChatbot.Web.Controllers;

[Authorize(Policy = AppPolicies.SubjectLeaderOnly)]
public sealed class DocumentsController(
    IDocumentService documentService,
    ICourseService courseService,
    IUserContext userContext) : Controller
{
    private static readonly Dictionary<string, DocumentType> AllowedExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        [".pdf"] = DocumentType.Pdf,
        [".docx"] = DocumentType.Docx,
        [".pptx"] = DocumentType.Slide
    };

    private const long MaxFileSizeBytes = 25 * 1024 * 1024; // 25 MB

    // GET: /Documents?courseId=1
    public async Task<IActionResult> Index(int courseId, CancellationToken cancellationToken)
    {
        if (courseId <= 0)
        {
            return RedirectToAction("Index", "Courses");
        }

        var course = await GetManagedCourseAsync(courseId, cancellationToken);
        if (course is null)
        {
            return NotFound();
        }

        var documents = await documentService.GetAllAsync(courseId, cancellationToken);
        return View(new CourseDocumentsViewModel(course, documents));
    }

    // GET: /Documents/Details/{id}
    public async Task<IActionResult> Details(Guid id, CancellationToken cancellationToken)
    {
        try
        {
            var document = await documentService.GetByIdAsync(id, cancellationToken);
            var course = await GetManagedCourseAsync(document.CourseId, cancellationToken);
            if (course is null)
            {
                return NotFound();
            }

            ViewData["Course"] = course;
            return View(document);
        }
        catch (DocumentNotFoundException)
        {
            return NotFound();
        }
    }

    // GET: /Documents/Upload
    public async Task<IActionResult> Upload(int courseId, CancellationToken cancellationToken)
    {
        var course = await GetManagedCourseAsync(courseId, cancellationToken);
        if (course is null)
        {
            return NotFound();
        }

        ViewData["Course"] = course;
        return View(new DocumentUploadViewModel { CourseId = courseId });
    }

    // POST: /Documents/Upload
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Upload(DocumentUploadViewModel vm, CancellationToken cancellationToken)
    {
        var course = await GetManagedCourseAsync(vm.CourseId, cancellationToken);
        if (course is null)
        {
            return NotFound();
        }

        ViewData["Course"] = course;
        if (!AllowedExtensions.TryGetValue(Path.GetExtension(vm.File?.FileName ?? string.Empty), out var fileType))
        {
            ModelState.AddModelError(nameof(vm.File), "Chỉ chấp nhận tệp .pdf, .docx và .pptx.");
        }
        else if (vm.File!.Length == 0)
        {
            ModelState.AddModelError(nameof(vm.File), "Tệp được chọn không có nội dung.");
        }
        else if (vm.File.Length > MaxFileSizeBytes)
        {
            ModelState.AddModelError(nameof(vm.File), "Tệp vượt quá giới hạn 25 MB.");
        }

        if (!ModelState.IsValid)
        {
            return View(vm);
        }

        await using var stream = vm.File!.OpenReadStream();
        var document = await documentService.UploadAsync(
            vm.CourseId,
            vm.Title,
            vm.Chapter,
            vm.File.FileName,
            fileType,
            stream,
            vm.File.Length,
            userContext.UserId,
            userContext.DisplayName,
            cancellationToken);

        if (document.Status == DocumentStatus.Indexed)
        {
            TempData["Success"] = $"Đã upload và lập chỉ mục tài liệu \"{document.Title}\".";
        }
        else
        {
            TempData["Error"] = $"Đã nhận tài liệu nhưng lập chỉ mục thất bại: {document.ProcessingError}";
        }
        return RedirectToAction(nameof(Details), new { id = document.Id });
    }

    // POST: /Documents/Delete/{id}
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Delete(Guid id, CancellationToken cancellationToken)
    {
        try
        {
            var document = await documentService.GetByIdAsync(id, cancellationToken);
            var course = await GetManagedCourseAsync(document.CourseId, cancellationToken);
            if (course is null)
            {
                return NotFound();
            }

            await documentService.DeleteAsync(id, cancellationToken);
            TempData["Success"] = "Đã xóa tài liệu.";
            return RedirectToAction(nameof(Index), new { courseId = document.CourseId });
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
