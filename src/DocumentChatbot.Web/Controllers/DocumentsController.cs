using DocumentChatbot.Core.Application.Abstractions;
using DocumentChatbot.Core.Application.Exceptions;
using DocumentChatbot.Core.Domain;
using DocumentChatbot.Web.Models;
using DocumentChatbot.Web.Services;
using Microsoft.AspNetCore.Mvc;

namespace DocumentChatbot.Web.Controllers;

public sealed class DocumentsController(
    IDocumentService documentService,
    IUserContext userContext) : Controller
{
    private static readonly Dictionary<string, DocumentType> AllowedExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        [".pdf"] = DocumentType.Pdf,
        [".docx"] = DocumentType.Docx,
        [".pptx"] = DocumentType.Slide
    };

    private const long MaxFileSizeBytes = 25 * 1024 * 1024; // 25 MB

    // GET: /Documents
    public async Task<IActionResult> Index(CancellationToken cancellationToken)
    {
        var documents = await documentService.GetAllAsync(cancellationToken);
        return View(documents);
    }

    // GET: /Documents/Details/{id}
    public async Task<IActionResult> Details(Guid id, CancellationToken cancellationToken)
    {
        try
        {
            var document = await documentService.GetByIdAsync(id, cancellationToken);
            return View(document);
        }
        catch (DocumentNotFoundException)
        {
            return NotFound();
        }
    }

    // GET: /Documents/Upload
    public IActionResult Upload() => View(new DocumentUploadViewModel());

    // POST: /Documents/Upload
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Upload(DocumentUploadViewModel vm, CancellationToken cancellationToken)
    {
        if (!AllowedExtensions.TryGetValue(Path.GetExtension(vm.File?.FileName ?? string.Empty), out var fileType))
        {
            ModelState.AddModelError(nameof(vm.File), "Only .pdf, .docx and .pptx files are allowed.");
        }
        else if (vm.File!.Length == 0)
        {
            ModelState.AddModelError(nameof(vm.File), "The selected file is empty.");
        }
        else if (vm.File.Length > MaxFileSizeBytes)
        {
            ModelState.AddModelError(nameof(vm.File), "File exceeds the 25 MB size limit.");
        }

        if (!ModelState.IsValid)
        {
            return View(vm);
        }

        await using var stream = vm.File!.OpenReadStream();
        var document = await documentService.UploadAsync(
            vm.Title,
            vm.Chapter,
            vm.File.FileName,
            fileType,
            stream,
            vm.File.Length,
            userContext.UserId,
            userContext.UserId,
            cancellationToken);

        TempData["Success"] = $"Document \"{document.Title}\" uploaded and indexed successfully.";
        return RedirectToAction(nameof(Details), new { id = document.Id });
    }

    // POST: /Documents/Delete/{id}
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Delete(Guid id, CancellationToken cancellationToken)
    {
        try
        {
            await documentService.DeleteAsync(id, cancellationToken);
            TempData["Success"] = "Document was deleted.";
        }
        catch (DocumentNotFoundException)
        {
            return NotFound();
        }

        return RedirectToAction(nameof(Index));
    }
}
