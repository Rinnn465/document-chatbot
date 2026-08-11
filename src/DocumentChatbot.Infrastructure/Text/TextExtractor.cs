using System.Text;
using DocumentChatbot.Core.Application.Abstractions;
using DocumentChatbot.Core.Domain;
using DocumentFormat.OpenXml.Packaging;
using iText.Kernel.Pdf;
using iText.Kernel.Pdf.Canvas.Parser;

namespace DocumentChatbot.Infrastructure.Text;

/// <summary>
/// Extracts plain text from uploaded PDF/DOCX/PPTX files. Ported from
/// PRN222_Asm1's TextExtractor.cs.
/// </summary>
public sealed class TextExtractor : ITextExtractor
{
    public string Extract(Stream fileStream, DocumentType fileType)
    {
        return fileType switch
        {
            DocumentType.Pdf => ExtractPdf(fileStream),
            DocumentType.Docx => ExtractDocx(fileStream),
            DocumentType.Slide => ExtractSlide(fileStream),
            _ => throw new NotSupportedException($"Unsupported document type: {fileType}")
        };
    }

    private static string ExtractPdf(Stream fileStream)
    {
        using var pdfDocument = new PdfDocument(new PdfReader(fileStream));
        var sb = new StringBuilder();
        for (var page = 1; page <= pdfDocument.GetNumberOfPages(); page++)
        {
            var text = PdfTextExtractor.GetTextFromPage(pdfDocument.GetPage(page));
            sb.AppendLine(text);
        }

        return sb.ToString();
    }

    private static string ExtractDocx(Stream fileStream)
    {
        using var wordDocument = WordprocessingDocument.Open(fileStream, false);
        var body = wordDocument.MainDocumentPart?.Document?.Body;
        return body?.InnerText ?? string.Empty;
    }

    private static string ExtractSlide(Stream fileStream)
    {
        using var presentationDocument = DocumentFormat.OpenXml.Packaging.PresentationDocument.Open(fileStream, false);
        var presentationPart = presentationDocument.PresentationPart;
        if (presentationPart?.SlideParts is null)
        {
            return string.Empty;
        }

        var sb = new StringBuilder();
        foreach (var slidePart in presentationPart.SlideParts)
        {
            var texts = slidePart.Slide.Descendants<DocumentFormat.OpenXml.Drawing.Text>();
            foreach (var t in texts)
            {
                sb.Append(t.Text);
                sb.Append(' ');
            }

            sb.AppendLine();
        }

        return sb.ToString();
    }
}
