namespace OpportunityOS.Application.Import;

/// <summary>Extracts plain text from a PDF stream (no OCR). Implemented over PdfPig in Infrastructure.</summary>
public interface IPdfTextExtractor
{
    Task<string> ExtractTextAsync(Stream pdfStream, CancellationToken cancellationToken);
}

/// <summary>
/// Thrown when a PDF yields too little/no extractable text (e.g. a scanned image PDF).
/// Carries a user-friendly message safe to show in the UI.
/// </summary>
public sealed class PdfTextExtractionException : Exception
{
    public PdfTextExtractionException(string message) : base(message) { }
}
