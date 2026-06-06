using OpportunityOS.Contracts;

namespace OpportunityOS.Application.Import;

/// <summary>
/// Deterministic, layout-aware parser for the text extracted from a LinkedIn-exported profile PDF.
/// Best-effort and never lossy: a block it can't segment confidently is preserved as a partial
/// experience (raw text) and a warning, rather than discarded.
/// </summary>
public interface ILinkedInProfilePdfParser
{
    LinkedInProfileImportDto Parse(string extractedText, LinkedInPdfDetectionResult detection, out List<string> warnings);
}
