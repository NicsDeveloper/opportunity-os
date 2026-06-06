using System.Text;
using OpportunityOS.Application.Import;
using UglyToad.PdfPig;
using UglyToad.PdfPig.Content;

namespace OpportunityOS.Infrastructure.Import;

/// <summary>
/// Text extraction via UglyToad.PdfPig (Apache-2.0, managed, no OCR). LinkedIn exports a TWO-COLUMN
/// PDF (narrow left sidebar = contact/skills/certifications; wide right column = name/summary/
/// experience/education). Naive top-to-bottom reading interleaves the columns, so we split by X and
/// emit the MAIN (right) column first (all pages), then the SIDEBAR (left) — keeping each section's
/// lines contiguous for the parser. Single-column PDFs fall back to plain Y-ordered reading.
/// </summary>
public sealed class PdfPigTextExtractor : IPdfTextExtractor
{
    private const int MinChars = 500;
    private const double LineTolerance = 4.0;   // points; words within this Y delta share a line
    private const double SidebarFraction = 0.34; // left third ≈ LinkedIn sidebar
    private const string Friendly =
        "Não conseguimos ler o texto deste PDF. Confirme se ele foi exportado diretamente do LinkedIn e tente novamente.";

    public async Task<string> ExtractTextAsync(Stream pdfStream, CancellationToken cancellationToken)
    {
        using var ms = new MemoryStream();
        await pdfStream.CopyToAsync(ms, cancellationToken);

        var main = new StringBuilder();
        var sidebar = new StringBuilder();
        try
        {
            using var doc = PdfDocument.Open(ms.ToArray());
            foreach (var page in doc.GetPages())
            {
                cancellationToken.ThrowIfCancellationRequested();
                AppendPage(page, main, sidebar);
            }
        }
        catch (OperationCanceledException) { throw; }
        catch (Exception)
        {
            throw new PdfTextExtractionException(Friendly);
        }

        // Main column first (name → summary → experience → education), then the sidebar.
        var text = main.ToString();
        if (sidebar.Length > 0) text += "\n" + sidebar;

        if (text.Trim().Length < MinChars)
            throw new PdfTextExtractionException(Friendly);
        return text;
    }

    private static void AppendPage(Page page, StringBuilder main, StringBuilder sidebar)
    {
        var words = page.GetWords().Where(w => !string.IsNullOrWhiteSpace(w.Text)).ToList();
        if (words.Count == 0) return;

        var split = page.Width * SidebarFraction;
        var leftWords = words.Where(w => w.BoundingBox.Right <= split).ToList();
        var rightWords = words.Where(w => w.BoundingBox.Right > split).ToList();

        // Treat as single-column when the sidebar is too thin to be a real column.
        if (leftWords.Count < 4)
        {
            AppendLines(main, words);
            return;
        }
        AppendLines(main, rightWords);
        AppendLines(sidebar, leftWords);
    }

    private static void AppendLines(StringBuilder sb, List<Word> words)
    {
        var lines = new List<(double Y, List<Word> Words)>();
        foreach (var word in words)
        {
            var y = word.BoundingBox.Bottom;
            var bucket = lines.FirstOrDefault(l => Math.Abs(l.Y - y) <= LineTolerance);
            if (bucket.Words is null) { bucket = (y, new List<Word>()); lines.Add(bucket); }
            bucket.Words.Add(word);
        }
        foreach (var line in lines.OrderByDescending(l => l.Y))
            sb.AppendLine(string.Join(" ", line.Words.OrderBy(w => w.BoundingBox.Left).Select(w => w.Text)));
    }
}
