namespace OpportunityOS.Application.Matching;

/// <summary>
/// Controls the hybrid (heuristic + semantic) score. Off by default — when enabled and both the
/// profile and job have an embedding, the engine blends the cosine similarity into the overall score
/// (the transparent heuristic and its gates still apply).
/// </summary>
public sealed class SemanticMatchOptions
{
    public bool Enabled { get; set; }
    /// <summary>Weight of the semantic component in the blend (0..1). Heuristic gets (1 - weight).</summary>
    public double SemanticWeight { get; set; } = 0.4;
}
