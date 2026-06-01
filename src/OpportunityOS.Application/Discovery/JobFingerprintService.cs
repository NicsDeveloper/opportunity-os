using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;

namespace OpportunityOS.Application.Discovery;

/// <summary>
/// Deterministic fingerprint = SHA1 of normalized (title | company | location | seniority |
/// top-skills | description-hash). Accent/punctuation-insensitive, order-stable.
/// </summary>
public sealed partial class JobFingerprintService : IJobFingerprintService
{
    public string GenerateFingerprint(JobFingerprintInput input)
    {
        var skills = string.Join(",", (input.TopSkills ?? Enumerable.Empty<string>())
            .Select(Normalize).Where(s => s.Length > 0).OrderBy(s => s, StringComparer.Ordinal).Take(8));

        var descHash = string.IsNullOrWhiteSpace(input.DescriptionText)
            ? string.Empty
            : ShortHash(Normalize(input.DescriptionText));

        var basis = string.Join("|",
            Normalize(input.Title),
            Normalize(input.Company),
            Normalize(input.Location),
            Normalize(input.Seniority),
            skills,
            descHash);

        return Convert.ToHexString(SHA1.HashData(Encoding.UTF8.GetBytes(basis)))[..24].ToLowerInvariant();
    }

    private static string Normalize(string? s)
    {
        if (string.IsNullOrWhiteSpace(s)) return string.Empty;
        var lowered = s.Trim().ToLowerInvariant();
        var stripped = new string(lowered.Normalize(NormalizationForm.FormD)
            .Where(c => CharUnicodeInfo.GetUnicodeCategory(c) != UnicodeCategory.NonSpacingMark).ToArray());
        var alnum = NonAlnumRegex().Replace(stripped, " ");
        return WhitespaceRegex().Replace(alnum, " ").Trim();
    }

    private static string ShortHash(string s) =>
        Convert.ToHexString(SHA1.HashData(Encoding.UTF8.GetBytes(s)))[..8].ToLowerInvariant();

    [GeneratedRegex(@"[^a-z0-9 ]")]
    private static partial Regex NonAlnumRegex();

    [GeneratedRegex(@"\s+")]
    private static partial Regex WhitespaceRegex();
}
