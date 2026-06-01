using OpportunityOS.Domain.Enums;

namespace OpportunityOS.Application.Discovery;

public sealed record SourceClassificationResult(
    SourceType SourceType,
    string SourceName,
    int SourceConfidenceScore,
    bool RequiresManualValidation,
    string Reason);

/// <summary>
/// Classifies the QUALITY of a source without killing volume: a weak source isn't
/// discarded, it's labelled (SourceType + confidence + manual-review flag). Pure heuristic,
/// no network/LLM. LinkedIn-indexed results are always manual-review-only.
/// </summary>
public interface ISourceClassifierService
{
    SourceClassificationResult Classify(string url, string? title, string? snippet);
}
