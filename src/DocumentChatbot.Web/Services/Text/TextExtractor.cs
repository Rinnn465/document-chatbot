using System.Text;
using DocumentChatbot.Web.Models;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Presentation;
using iText.Kernel.Pdf;
using iText.Kernel.Pdf.Canvas.Parser;
using A = DocumentFormat.OpenXml.Drawing;
using P = DocumentFormat.OpenXml.Presentation;
using W = DocumentFormat.OpenXml.Wordprocessing;

namespace DocumentChatbot.Web.Services;

/// <summary>
/// Extracts logical sections from PDF, DOCX and PPTX files. Page and slide
/// boundaries are retained so the RAG service can produce precise citations.
/// </summary>
public sealed class TextExtractor : ITextExtractor
{
    public ExtractedDocument Extract(Stream fileStream, DocumentType fileType)
    {
        if (fileStream.CanSeek)
        {
            fileStream.Position = 0;
        }

        return fileType switch
        {
            DocumentType.Pdf => ExtractPdf(fileStream),
            DocumentType.Docx => ExtractDocx(fileStream),
            DocumentType.Slide => ExtractPresentation(fileStream),
            _ => throw new NotSupportedException($"Unsupported document type: {fileType}")
        };
    }

    private static ExtractedDocument ExtractPdf(Stream fileStream)
    {
        using var pdfDocument = new PdfDocument(new PdfReader(fileStream));
        var sections = new List<ExtractedDocumentSection>();

        for (var pageNumber = 1; pageNumber <= pdfDocument.GetNumberOfPages(); pageNumber++)
        {
            var content = NormalizeLines(PdfTextExtractor.GetTextFromPage(pdfDocument.GetPage(pageNumber)));
            if (!string.IsNullOrWhiteSpace(content))
            {
                sections.Add(new ExtractedDocumentSection("page", pageNumber, null, content));
            }
        }

        return new ExtractedDocument(sections);
    }

    private static ExtractedDocument ExtractDocx(Stream fileStream)
    {
        using var wordDocument = WordprocessingDocument.Open(fileStream, false);
        var body = wordDocument.MainDocumentPart?.Document?.Body;
        if (body is null)
        {
            return new ExtractedDocument([]);
        }

        var lines = body.Descendants<W.Paragraph>()
            .Select(paragraph => string.Concat(paragraph.Descendants<W.Text>().Select(text => text.Text)).Trim())
            .Where(line => line.Length > 0);
        var content = NormalizeLines(string.Join(Environment.NewLine, lines));

        return string.IsNullOrWhiteSpace(content)
            ? new ExtractedDocument([])
            : new ExtractedDocument([new ExtractedDocumentSection("document", null, null, content)]);
    }

    private static ExtractedDocument ExtractPresentation(Stream fileStream)
    {
        using var presentationDocument = PresentationDocument.Open(fileStream, false);
        var presentationPart = presentationDocument.PresentationPart;
        var slideIds = presentationPart?.Presentation?.SlideIdList?.Elements<SlideId>().ToList();
        if (presentationPart is null || slideIds is null || slideIds.Count == 0)
        {
            return new ExtractedDocument([]);
        }

        var sections = new List<ExtractedDocumentSection>(slideIds.Count);
        for (var slideIndex = 0; slideIndex < slideIds.Count; slideIndex++)
        {
            var relationshipId = slideIds[slideIndex].RelationshipId?.Value;
            if (string.IsNullOrWhiteSpace(relationshipId) ||
                presentationPart.GetPartById(relationshipId) is not SlidePart slidePart)
            {
                continue;
            }

            var titleShape = FindTitleShape(slidePart);
            var title = titleShape is null ? null : ExtractShapeText(titleShape);
            var bodyLines = ExtractSlideLines(slidePart, titleShape);
            var notesLines = ExtractSpeakerNotes(slidePart);

            var contentBuilder = new StringBuilder();
            AppendLines(contentBuilder, bodyLines);
            if (notesLines.Count > 0)
            {
                if (contentBuilder.Length > 0)
                {
                    contentBuilder.AppendLine();
                }

                contentBuilder.AppendLine("Speaker notes:");
                AppendLines(contentBuilder, notesLines);
            }

            var content = NormalizeLines(contentBuilder.ToString());
            if (string.IsNullOrWhiteSpace(content) && string.IsNullOrWhiteSpace(title))
            {
                continue;
            }

            sections.Add(new ExtractedDocumentSection(
                "slide",
                slideIndex + 1,
                NullIfWhiteSpace(title),
                content));
        }

        return new ExtractedDocument(sections);
    }

    private static P.Shape? FindTitleShape(SlidePart slidePart) =>
        slidePart.Slide.Descendants<P.Shape>().FirstOrDefault(shape =>
        {
            var placeholder = shape.NonVisualShapeProperties?
                .ApplicationNonVisualDrawingProperties?
                .GetFirstChild<P.PlaceholderShape>();
            var placeholderType = placeholder?.Type?.Value;
            return placeholderType == PlaceholderValues.Title ||
                   placeholderType == PlaceholderValues.CenteredTitle;
        });

    private static List<string> ExtractSlideLines(SlidePart slidePart, P.Shape? titleShape)
    {
        var lines = new List<string>();
        foreach (var paragraph in slidePart.Slide.Descendants<A.Paragraph>())
        {
            if (titleShape is not null && paragraph.Ancestors<P.Shape>().Any(shape => ReferenceEquals(shape, titleShape)))
            {
                continue;
            }

            var line = ExtractParagraphText(paragraph);
            if (!string.IsNullOrWhiteSpace(line))
            {
                lines.Add(line);
            }
        }

        return RemoveConsecutiveDuplicates(lines);
    }

    private static List<string> ExtractSpeakerNotes(SlidePart slidePart)
    {
        var notesSlide = slidePart.NotesSlidePart?.NotesSlide;
        if (notesSlide is null)
        {
            return [];
        }

        var lines = new List<string>();
        foreach (var shape in notesSlide.Descendants<P.Shape>())
        {
            var placeholder = shape.NonVisualShapeProperties?
                .ApplicationNonVisualDrawingProperties?
                .GetFirstChild<P.PlaceholderShape>();
            if (placeholder?.Type?.Value != PlaceholderValues.Body)
            {
                continue;
            }

            foreach (var paragraph in shape.Descendants<A.Paragraph>())
            {
                var line = ExtractParagraphText(paragraph);
                if (!string.IsNullOrWhiteSpace(line))
                {
                    lines.Add(line);
                }
            }
        }

        return RemoveConsecutiveDuplicates(lines);
    }

    private static string ExtractShapeText(P.Shape shape) =>
        NormalizeInline(string.Join(" ", shape.Descendants<A.Paragraph>()
            .Select(ExtractParagraphText)
            .Where(text => text.Length > 0)));

    private static string ExtractParagraphText(A.Paragraph paragraph) =>
        NormalizeInline(string.Concat(paragraph.Descendants<A.Text>().Select(text => text.Text)));

    private static void AppendLines(StringBuilder builder, IEnumerable<string> lines)
    {
        foreach (var line in lines)
        {
            builder.AppendLine(line);
        }
    }

    private static List<string> RemoveConsecutiveDuplicates(IEnumerable<string> lines)
    {
        var result = new List<string>();
        foreach (var line in lines)
        {
            if (result.Count == 0 || !string.Equals(result[^1], line, StringComparison.Ordinal))
            {
                result.Add(line);
            }
        }

        return result;
    }

    private static string NormalizeInline(string value) =>
        string.Join(" ", value.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries));

    private static string NormalizeLines(string value)
    {
        var lines = value.Replace("\r\n", "\n", StringComparison.Ordinal)
            .Replace('\r', '\n')
            .Split('\n')
            .Select(NormalizeInline)
            .Where(line => line.Length > 0);
        return string.Join(Environment.NewLine, lines);
    }

    private static string? NullIfWhiteSpace(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
