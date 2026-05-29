using System.Net;
using System.Text;

namespace OpportunityOS.Application.Digest;

/// <summary>Renders digest items into Markdown (preview) and HTML (email body).</summary>
public static class DigestRenderer
{
    public static DigestPreview Render(IReadOnlyList<OpportunityDigestItem> items)
    {
        var ordered = items.OrderByDescending(i => i.OverallScore).ToList();
        var strategic = ordered.Count(i => i.Recommendation == "Strategic");
        var prioritize = ordered.Count(i => i.Recommendation == "Prioritize");
        var apply = ordered.Count(i => i.Recommendation == "Apply");

        var subject = ordered.Count == 0
            ? "Opportunity OS — Digest diário (sem oportunidades relevantes)"
            : $"Opportunity OS — Digest diário ({ordered.Count} oportunidades)";

        return new DigestPreview(
            subject, BuildMarkdown(ordered, strategic, prioritize, apply),
            BuildHtml(ordered, strategic, prioritize, apply),
            ordered.Count, strategic, prioritize, apply);
    }

    private static string BuildMarkdown(IReadOnlyList<OpportunityDigestItem> items, int strategic, int prioritize, int apply)
    {
        var sb = new StringBuilder();
        sb.AppendLine("# Opportunity OS — Digest diário").AppendLine();
        sb.AppendLine("## Resumo").AppendLine();
        sb.AppendLine($"Foram encontradas {items.Count} oportunidades.");
        sb.AppendLine($"{strategic} estratégicas. {prioritize} prioritárias. {apply} boas.").AppendLine();

        if (items.Count == 0)
        {
            sb.AppendLine("_Nenhuma oportunidade relevante hoje._");
            return sb.ToString();
        }

        var i = 1;
        foreach (var it in items)
        {
            sb.AppendLine("---").AppendLine();
            sb.AppendLine($"## {i}. {it.CompanyName} — {it.JobTitle}").AppendLine();
            sb.AppendLine($"Score: {it.OverallScore}/100  ");
            sb.AppendLine($"Recomendação: {it.Recommendation}  ");
            sb.AppendLine($"Link: {it.JobUrl}").AppendLine();
            sb.AppendLine("### Por que vale ação").AppendLine().AppendLine(Or(it.Rationale, "—")).AppendLine();
            sb.AppendLine("### Pontos fortes").AppendLine().AppendLine(Bullets(it.Strengths)).AppendLine();
            sb.AppendLine("### Riscos").AppendLine().AppendLine(Bullets(it.Risks)).AppendLine();
            if (!string.IsNullOrWhiteSpace(it.LinkedInMessage))
                sb.AppendLine("### Mensagem curta sugerida").AppendLine().AppendLine(it.LinkedInMessage).AppendLine();
            if (!string.IsNullOrWhiteSpace(it.CoverLetter))
                sb.AppendLine("### Carta de apresentação").AppendLine().AppendLine(it.CoverLetter).AppendLine();
            if (!string.IsNullOrWhiteSpace(it.CvTailoringNotes))
                sb.AppendLine("### Ajustes sugeridos no CV").AppendLine().AppendLine(it.CvTailoringNotes).AppendLine();
            i++;
        }
        return sb.ToString();
    }

    private static string BuildHtml(IReadOnlyList<OpportunityDigestItem> items, int strategic, int prioritize, int apply)
    {
        var sb = new StringBuilder();
        sb.Append("<div style=\"font-family:Arial,Helvetica,sans-serif;color:#1a1a1a\">");
        sb.Append("<h1>Opportunity OS — Digest diário</h1>");
        sb.Append($"<p>Foram encontradas <strong>{items.Count}</strong> oportunidades. ")
          .Append($"{strategic} estratégicas · {prioritize} prioritárias · {apply} boas.</p>");

        if (items.Count == 0)
        {
            sb.Append("<p><em>Nenhuma oportunidade relevante hoje.</em></p></div>");
            return sb.ToString();
        }

        var i = 1;
        foreach (var it in items)
        {
            sb.Append("<hr/>");
            sb.Append($"<h2>{i}. {E(it.CompanyName)} — {E(it.JobTitle)}</h2>");
            sb.Append($"<p><strong>Score:</strong> {it.OverallScore}/100 &nbsp;|&nbsp; ")
              .Append($"<strong>Recomendação:</strong> {E(it.Recommendation)}<br/>")
              .Append($"<a href=\"{E(it.JobUrl)}\">{E(it.JobUrl)}</a></p>");
            sb.Append($"<p><strong>Por que vale ação:</strong> {E(it.Rationale)}</p>");
            sb.Append("<p><strong>Pontos fortes:</strong></p>").Append(HtmlList(it.Strengths));
            sb.Append("<p><strong>Riscos:</strong></p>").Append(HtmlList(it.Risks));
            if (!string.IsNullOrWhiteSpace(it.LinkedInMessage))
                sb.Append($"<p><strong>Mensagem curta:</strong><br/>{E(it.LinkedInMessage)}</p>");
            if (!string.IsNullOrWhiteSpace(it.CoverLetter))
                sb.Append($"<p><strong>Carta:</strong><br/>{E(it.CoverLetter)}</p>");
            if (!string.IsNullOrWhiteSpace(it.CvTailoringNotes))
                sb.Append($"<p><strong>Ajustes no CV:</strong><br/>{E(it.CvTailoringNotes)}</p>");
            i++;
        }
        sb.Append("</div>");
        return sb.ToString();
    }

    private static string Bullets(List<string> items) =>
        items.Count == 0 ? "- —" : string.Join("\n", items.Select(x => $"- {x}"));

    private static string HtmlList(List<string> items) =>
        items.Count == 0 ? "<ul><li>—</li></ul>" : "<ul>" + string.Concat(items.Select(x => $"<li>{E(x)}</li>")) + "</ul>";

    private static string Or(string value, string fallback) => string.IsNullOrWhiteSpace(value) ? fallback : value;
    private static string E(string s) => WebUtility.HtmlEncode(s ?? string.Empty);
}
