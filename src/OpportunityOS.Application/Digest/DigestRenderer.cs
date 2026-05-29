using System.Net;
using System.Text;

namespace OpportunityOS.Application.Digest;

/// <summary>
/// Renders digest items into Markdown (preview/text) and HTML (email body).
/// The HTML is intentionally lean and scannable — built for triage, not for
/// dumping every field. Heavy content (full cover letter, CV notes) stays in
/// the API/preview, not in the e-mail.
/// </summary>
public static class DigestRenderer
{
    public static DigestPreview Render(IReadOnlyList<OpportunityDigestItem> items, string? candidateName = null)
    {
        var ordered = items.OrderByDescending(i => i.OverallScore).ToList();
        var strategic = ordered.Count(i => i.Recommendation == "Strategic");
        var prioritize = ordered.Count(i => i.Recommendation == "Prioritize");
        var apply = ordered.Count(i => i.Recommendation == "Apply");
        var firstName = FirstName(candidateName);

        var subject = ordered.Count == 0
            ? "Opportunity OS — Digest diário (nenhuma oportunidade relevante)"
            : $"Opportunity OS — Digest diário ({Plural(ordered.Count, "oportunidade", "oportunidades")})";

        return new DigestPreview(
            subject,
            BuildMarkdown(ordered, strategic, prioritize, apply, firstName),
            BuildHtml(ordered, strategic, prioritize, apply, firstName),
            ordered.Count, strategic, prioritize, apply);
    }

    private static string Greeting(string? firstName) =>
        firstName is null ? "Aqui estão suas oportunidades de hoje." : $"Olá, {firstName}! Aqui estão suas oportunidades de hoje.";

    private static string? FirstName(string? fullName)
    {
        if (string.IsNullOrWhiteSpace(fullName)) return null;
        var first = fullName.Trim().Split(' ', StringSplitOptions.RemoveEmptyEntries)[0];
        return string.IsNullOrWhiteSpace(first) ? null : first;
    }

    // ---------- Markdown (preview / plain text) ----------

    private static string BuildMarkdown(IReadOnlyList<OpportunityDigestItem> items, int strategic, int prioritize, int apply, string? firstName)
    {
        var sb = new StringBuilder();
        sb.AppendLine("# Opportunity OS — Digest diário").AppendLine();
        sb.AppendLine(Greeting(firstName)).AppendLine();

        if (items.Count == 0)
        {
            sb.AppendLine("_Nenhuma oportunidade relevante hoje._");
            return sb.ToString();
        }

        sb.AppendLine($"**{Plural(items.Count, "oportunidade", "oportunidades")}** · {SummaryLine(strategic, prioritize, apply)}").AppendLine();

        var i = 1;
        foreach (var it in items)
        {
            sb.AppendLine($"### {i}. {it.CompanyName} — {it.JobTitle}");
            sb.AppendLine($"`{it.OverallScore}/100` · **{it.Recommendation}** · [Ver vaga]({it.JobUrl})").AppendLine();
            if (!string.IsNullOrWhiteSpace(it.Rationale))
                sb.AppendLine(Truncate(it.Rationale, 240)).AppendLine();
            foreach (var s in it.Strengths.Take(3)) sb.AppendLine($"- ✅ {s}");
            foreach (var r in it.Risks.Take(2)) sb.AppendLine($"- ⚠️ {r}");
            if (!string.IsNullOrWhiteSpace(it.LinkedInMessage))
            {
                var lang = LanguageLabel(it.OutreachLanguage);
                sb.AppendLine().AppendLine($"_Mensagem sugerida{(lang is null ? "" : $" ({lang})")}:_");
                sb.AppendLine($"> {it.LinkedInMessage}");
            }
            sb.AppendLine();
            i++;
        }
        return sb.ToString();
    }

    // ---------- HTML (email) ----------

    private static string BuildHtml(IReadOnlyList<OpportunityDigestItem> items, int strategic, int prioritize, int apply, string? firstName)
    {
        const string font = "font-family:-apple-system,Segoe UI,Roboto,Helvetica,Arial,sans-serif";
        var sb = new StringBuilder();

        // Hidden preheader — controls the snippet Gmail shows next to the subject.
        var preheader = items.Count == 0
            ? "Nenhuma oportunidade relevante hoje."
            : $"{Plural(items.Count, "oportunidade", "oportunidades")} · {SummaryLine(strategic, prioritize, apply)}";
        sb.Append($"<div style=\"display:none;max-height:0;overflow:hidden;opacity:0\">{E(preheader)}</div>");

        sb.Append($"<div style=\"{font};max-width:600px;margin:0 auto;padding:8px;color:#1f2937;background:#f6f7f9\">");

        // Header
        sb.Append("<div style=\"padding:20px 4px 8px\">")
          .Append("<div style=\"font-size:20px;font-weight:700;color:#111827\">Opportunity OS</div>")
          .Append($"<div style=\"font-size:14px;color:#374151;margin-top:2px\">{E(Greeting(firstName))}</div>")
          .Append("</div>");

        if (items.Count == 0)
        {
            sb.Append("<div style=\"background:#fff;border:1px solid #e5e7eb;border-radius:12px;padding:24px;text-align:center;color:#6b7280\">")
              .Append("Nenhuma oportunidade relevante hoje.</div></div>");
            return sb.ToString();
        }

        // Summary chips
        sb.Append("<div style=\"padding:4px 4px 12px;font-size:13px;color:#374151\">")
          .Append($"<strong>{Plural(items.Count, "oportunidade", "oportunidades")}</strong>&nbsp;&nbsp;")
          .Append(SummaryChips(strategic, prioritize, apply))
          .Append("</div>");

        var i = 1;
        foreach (var it in items)
        {
            var (bg, fg) = RecColors(it.Recommendation);
            sb.Append("<div style=\"background:#fff;border:1px solid #e5e7eb;border-radius:12px;padding:16px;margin:0 0 12px\">");

            // Title
            sb.Append($"<div style=\"font-size:12px;color:#6b7280;margin-bottom:2px\">{i}. {E(it.CompanyName)}</div>");
            sb.Append($"<div style=\"font-size:16px;font-weight:700;color:#111827;margin-bottom:8px\">{E(it.JobTitle)}</div>");

            // Badges: score (tiered colour) + recommendation
            sb.Append("<div style=\"margin-bottom:10px\">")
              .Append(Pill($"{it.OverallScore}/100", "#ffffff", ScoreColor(it.OverallScore)))
              .Append("&nbsp;")
              .Append(Pill(it.Recommendation, fg, bg))
              .Append("</div>");

            // Why it matters (short)
            if (!string.IsNullOrWhiteSpace(it.Rationale))
                sb.Append($"<div style=\"font-size:13px;line-height:1.5;color:#374151;margin-bottom:10px\">{E(Truncate(it.Rationale, 240))}</div>");

            // Top strengths / risks (compact)
            var strengths = it.Strengths.Take(3).ToList();
            if (strengths.Count > 0)
                sb.Append(MiniList("Destaques", strengths, "#047857"));
            var risks = it.Risks.Take(2).ToList();
            if (risks.Count > 0)
                sb.Append(MiniList("Atenção", risks, "#b45309"));

            // Suggested message (actionable, copy-paste) + language hint
            if (!string.IsNullOrWhiteSpace(it.LinkedInMessage))
            {
                var lang = LanguageLabel(it.OutreachLanguage);
                var header = lang is null ? "Mensagem sugerida" : $"Mensagem sugerida · {lang}";
                sb.Append("<div style=\"background:#f3f4f6;border-radius:8px;padding:10px 12px;margin:10px 0;font-size:13px;color:#374151\">")
                  .Append($"<div style=\"font-size:11px;text-transform:uppercase;letter-spacing:.04em;color:#6b7280;margin-bottom:4px\">{E(header)}</div>")
                  .Append(E(it.LinkedInMessage))
                  .Append("</div>");
            }

            // CTA
            sb.Append($"<a href=\"{E(it.JobUrl)}\" style=\"display:inline-block;background:#2563eb;color:#fff;text-decoration:none;")
              .Append("font-size:14px;font-weight:600;padding:9px 16px;border-radius:8px;margin-top:4px\">Ver vaga &rarr;</a>");

            sb.Append("</div>");
            i++;
        }

        sb.Append("<div style=\"text-align:center;color:#9ca3af;font-size:11px;padding:8px 0 16px\">")
          .Append("Gerado pelo Opportunity OS · revise antes de enviar qualquer mensagem.</div>");
        sb.Append("</div>");
        return sb.ToString();
    }

    // ---------- helpers ----------

    private static string Pill(string text, string fg, string bg) =>
        $"<span style=\"display:inline-block;background:{bg};color:{fg};font-size:12px;font-weight:600;" +
        $"padding:3px 10px;border-radius:999px\">{E(text)}</span>";

    private static string MiniList(string label, List<string> items, string color)
    {
        var sb = new StringBuilder();
        sb.Append($"<div style=\"font-size:11px;text-transform:uppercase;letter-spacing:.04em;color:{color};margin:8px 0 2px\">{E(label)}</div>");
        sb.Append("<ul style=\"margin:0 0 4px;padding-left:18px;font-size:13px;color:#374151;line-height:1.45\">");
        foreach (var it in items) sb.Append($"<li>{E(it)}</li>");
        sb.Append("</ul>");
        return sb.ToString();
    }

    private static (string Bg, string Fg) RecColors(string recommendation) => recommendation switch
    {
        "Strategic" => ("#ede9fe", "#5b21b6"),
        "Prioritize" => ("#dcfce7", "#166534"),
        "Apply" => ("#dbeafe", "#1e40af"),
        "SaveForLater" => ("#fef9c3", "#854d0e"),
        _ => ("#f3f4f6", "#374151")
    };

    private static string ScoreColor(int score) => score switch
    {
        >= 90 => "#6d28d9", // violet
        >= 75 => "#16a34a", // green
        >= 60 => "#2563eb", // blue
        _ => "#6b7280"      // gray
    };

    private static string? LanguageLabel(string? code) => code?.ToLowerInvariant() switch
    {
        null or "" => null,
        "en" or "en-us" or "en-gb" => "inglês",
        "pt" or "pt-br" => "português",
        "es" or "es-es" => "espanhol",
        _ => code
    };

    private static string SummaryLine(int strategic, int prioritize, int apply)
    {
        var parts = new List<string>();
        if (strategic > 0) parts.Add(Plural(strategic, "estratégica", "estratégicas"));
        if (prioritize > 0) parts.Add(Plural(prioritize, "prioritária", "prioritárias"));
        if (apply > 0) parts.Add(Plural(apply, "boa", "boas"));
        return parts.Count == 0 ? "—" : string.Join(" · ", parts);
    }

    private static string SummaryChips(int strategic, int prioritize, int apply)
    {
        var sb = new StringBuilder();
        if (strategic > 0) { var (b, f) = RecColors("Strategic"); sb.Append(Pill(Plural(strategic, "estratégica", "estratégicas"), f, b)).Append("&nbsp;"); }
        if (prioritize > 0) { var (b, f) = RecColors("Prioritize"); sb.Append(Pill(Plural(prioritize, "prioritária", "prioritárias"), f, b)).Append("&nbsp;"); }
        if (apply > 0) { var (b, f) = RecColors("Apply"); sb.Append(Pill(Plural(apply, "boa", "boas"), f, b)); }
        return sb.ToString();
    }

    private static string Plural(int n, string singular, string plural) =>
        n == 1 ? $"1 {singular}" : $"{n} {plural}";

    private static string Truncate(string s, int max) =>
        string.IsNullOrEmpty(s) || s.Length <= max ? s : s[..max].TrimEnd() + "…";

    private static string E(string s) => WebUtility.HtmlEncode(s ?? string.Empty);
}
