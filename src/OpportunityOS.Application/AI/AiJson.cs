using System.Text.Json;
using OpportunityOS.Domain.Entities;

namespace OpportunityOS.Application.AI;

/// <summary>Serialization helpers + tolerant JSON parsing for LLM I/O.</summary>
public static class AiJson
{
    private static readonly JsonSerializerOptions Options = new(JsonSerializerDefaults.Web)
    {
        WriteIndented = false
    };

    public static string SerializeProfile(CandidateProfile p) => JsonSerializer.Serialize(new
    {
        p.FullName, p.Headline, p.Summary, p.Location, p.Seniority, p.PreferredLanguage,
        p.CoreSkills, p.SecondarySkills, p.Domains, p.PreferredRoles, p.PreferredContractTypes,
        Experiences = p.Experiences.Select(e => new { e.Company, e.Role, e.Period, e.Technologies, e.Domains, e.Achievements })
    }, Options);

    public static string SerializeJob(JobPosting j) => JsonSerializer.Serialize(new
    {
        j.Title, j.Department, j.Location, j.WorkMode, j.Seniority, j.Language,
        j.ExtractedSkills, j.ExtractedDomains, Description = j.DescriptionText
    }, Options);

    public static string SerializeAnalysis(JobAnalysisResult a) => JsonSerializer.Serialize(a, Options);

    public static string SerializeMatch(OpportunityMatch m) => JsonSerializer.Serialize(new
    {
        m.OverallScore, m.TechnicalScore, m.DomainScore, m.SeniorityScore, m.LocationScore,
        m.LanguageScore, Recommendation = m.Recommendation.ToString(), m.Strengths, m.Risks,
        m.MissingRequirements, m.Rationale
    }, Options);

    /// <summary>
    /// Parse JSON into <typeparamref name="T"/>, tolerating ```json fences and
    /// leading/trailing prose. Returns false (with reason) instead of throwing.
    /// </summary>
    public static bool TryDeserialize<T>(string raw, out T? value, out string? error)
    {
        value = default;
        error = null;
        var json = ExtractJson(raw);
        if (string.IsNullOrWhiteSpace(json))
        {
            error = "empty or no JSON object found";
            return false;
        }
        try
        {
            value = JsonSerializer.Deserialize<T>(json, Options);
            if (value is null)
            {
                error = "deserialized to null";
                return false;
            }
            return true;
        }
        catch (JsonException ex)
        {
            error = ex.Message;
            return false;
        }
    }

    /// <summary>Slice from the first '{' / '[' to its matching last '}' / ']'.</summary>
    private static string ExtractJson(string raw)
    {
        if (string.IsNullOrWhiteSpace(raw)) return string.Empty;
        var start = raw.IndexOfAny(new[] { '{', '[' });
        if (start < 0) return string.Empty;
        var open = raw[start];
        var close = open == '{' ? '}' : ']';
        var end = raw.LastIndexOf(close);
        return end > start ? raw[start..(end + 1)] : string.Empty;
    }
}
