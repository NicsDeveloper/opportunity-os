namespace OpportunityOS.Application.Import;

public sealed record LinkedInPdfDetectionResult(
    bool IsLikelyLinkedInProfilePdf,
    int Confidence,
    List<string> Signals,
    List<string> MissingSignals);

/// <summary>Heuristic check that an extracted text looks like a LinkedIn-exported profile PDF.</summary>
public interface ILinkedInPdfProfileDetector
{
    LinkedInPdfDetectionResult Detect(string extractedText);
}
