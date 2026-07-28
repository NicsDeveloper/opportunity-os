namespace OpportunityOS.Application.AI;

/// <summary>
/// Turns text into a normalized vector for semantic similarity (cosine). A real model
/// (e.g. OpenAI text-embedding-3-small) captures synonyms/adjacency; the deterministic hashing
/// fallback (no API key) gives a lexical vector that is free, offline and testable.
/// </summary>
public interface IEmbeddingProvider
{
    string ModelName { get; }
    int Dimensions { get; }
    /// <summary>True when a real model is configured (vs the deterministic fallback).</summary>
    bool IsRealModel { get; }

    /// <summary>L2-normalized embedding of <paramref name="text"/>, or null on failure.</summary>
    Task<float[]?> EmbedAsync(string text, CancellationToken cancellationToken);
}

/// <summary>Cosine similarity for already-L2-normalized vectors (falls back to full cosine otherwise).</summary>
public static class VectorMath
{
    public static double Cosine(IReadOnlyList<float> a, IReadOnlyList<float> b)
    {
        if (a.Count == 0 || a.Count != b.Count) return 0;
        double dot = 0, na = 0, nb = 0;
        for (var i = 0; i < a.Count; i++) { dot += a[i] * b[i]; na += a[i] * a[i]; nb += b[i] * b[i]; }
        if (na == 0 || nb == 0) return 0;
        return Math.Clamp(dot / (Math.Sqrt(na) * Math.Sqrt(nb)), -1, 1);
    }
}
