namespace OpportunityOS.Application.Companies;

public sealed record CsvCompanyRow(
    string Name,
    string? WebsiteUrl,
    string? CareersUrl,
    string? Industry,
    string? Country);

/// <summary>
/// Parses a simple companies CSV. Columns (header optional, case-insensitive):
/// name, websiteUrl, careersUrl, industry, country. Only <c>name</c> is required.
/// Pure/dependency-free so it's easy to unit test.
/// </summary>
public static class CsvCompanyParser
{
    public static IReadOnlyList<CsvCompanyRow> Parse(string csv)
    {
        var rows = new List<CsvCompanyRow>();
        if (string.IsNullOrWhiteSpace(csv)) return rows;

        var lines = csv.Replace("\r\n", "\n").Split('\n', StringSplitOptions.RemoveEmptyEntries);
        var start = 0;
        if (lines.Length > 0 && lines[0].Replace(" ", "").StartsWith("name,", StringComparison.OrdinalIgnoreCase))
            start = 1; // skip header

        for (var i = start; i < lines.Length; i++)
        {
            var cells = SplitCsvLine(lines[i]);
            var name = Cell(cells, 0);
            if (string.IsNullOrWhiteSpace(name)) continue;

            rows.Add(new CsvCompanyRow(
                name.Trim(),
                Cell(cells, 1), Cell(cells, 2), Cell(cells, 3), Cell(cells, 4)));
        }
        return rows;
    }

    private static string? Cell(string[] cells, int i)
    {
        if (i >= cells.Length) return null;
        var v = cells[i].Trim().Trim('"');
        return string.IsNullOrWhiteSpace(v) ? null : v;
    }

    private static string[] SplitCsvLine(string line)
    {
        var result = new List<string>();
        var sb = new System.Text.StringBuilder();
        var inQuotes = false;
        foreach (var ch in line)
        {
            if (ch == '"') inQuotes = !inQuotes;
            else if (ch == ',' && !inQuotes) { result.Add(sb.ToString()); sb.Clear(); }
            else sb.Append(ch);
        }
        result.Add(sb.ToString());
        return result.ToArray();
    }
}
