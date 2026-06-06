using System.Text;
using System.Text.RegularExpressions;

namespace OpportunityOS.Application.AI;

/// <summary>
/// Deterministic, offline, key-free embedding via the hashing trick (FNV-1a → fixed buckets) over
/// unigrams + bigrams, L2-normalized. Honest scope: this is a LEXICAL vector (rewards shared terms
/// and phrases, a bit more flexible than exact keyword match) — it is NOT true semantic understanding.
/// The real semantic jump comes from a model provider (OpenAI); this keeps the pipeline working and
/// testable when no API key is configured.
/// </summary>
public sealed class HashingEmbeddingProvider : IEmbeddingProvider
{
    public const string Model = "hashing-v1";
    private const int Dim = 256;
    private static readonly Regex Token = new(@"[a-z0-9#.+]{2,}", RegexOptions.Compiled);

    public string ModelName => Model;
    public int Dimensions => Dim;
    public bool IsRealModel => false;

    public Task<float[]?> EmbedAsync(string text, CancellationToken cancellationToken)
    {
        var vec = new float[Dim];
        var tokens = Token.Matches((text ?? string.Empty).ToLowerInvariant()).Select(m => m.Value).ToList();

        void Add(string term) => vec[(int)(Fnv1a(term) % Dim)] += 1f;
        for (var i = 0; i < tokens.Count; i++)
        {
            Add(tokens[i]);
            if (i + 1 < tokens.Count) Add(tokens[i] + " " + tokens[i + 1]); // bigram
        }

        double norm = Math.Sqrt(vec.Sum(v => (double)v * v));
        if (norm > 0) for (var i = 0; i < Dim; i++) vec[i] = (float)(vec[i] / norm);
        return Task.FromResult<float[]?>(vec);
    }

    private static uint Fnv1a(string s)
    {
        const uint offset = 2166136261, prime = 16777619;
        var hash = offset;
        foreach (var b in Encoding.UTF8.GetBytes(s)) { hash ^= b; hash *= prime; }
        return hash;
    }
}
