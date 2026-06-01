namespace OpportunityOS.Application.Bacen;

/// <summary>
/// Parses the official Bacen Pix participants CSV (semicolon-separated; a title
/// line, then a header, then rows whose first column is a row index). Pure and
/// dependency-free so it's unit-testable without HTTP.
///
/// Columns: [idx] ; Nome Reduzido ; ISPB ; CNPJ ; Tipo de Instituição ;
/// Autorizada pelo BCB ; Tipo de Participação no SPI ; Tipo de Participação no Pix ;
/// Modalidade de Participação no Pix ; Iniciação de Transação de Pagamento ;
/// Facilitador de serviço de Saque e Troco (FSS)
/// </summary>
public static class BacenCsvParser
{
    public sealed class MissingColumnsException(string message) : Exception(message);

    public static IReadOnlyList<BacenPixParticipantDto> Parse(string content)
    {
        var result = new List<BacenPixParticipantDto>();
        if (string.IsNullOrWhiteSpace(content)) return result;

        var lines = content.Replace("\r\n", "\n").Split('\n');
        var headerIndex = Array.FindIndex(lines, l => l.Contains("Nome Reduzido", StringComparison.OrdinalIgnoreCase));
        if (headerIndex < 0)
            throw new MissingColumnsException("Header row with 'Nome Reduzido' not found.");

        var header = Split(lines[headerIndex]);
        var col = MapColumns(header);

        for (var i = headerIndex + 1; i < lines.Length; i++)
        {
            if (string.IsNullOrWhiteSpace(lines[i])) continue;
            var cells = Split(lines[i]);

            var name = Get(cells, col["name"]);
            if (string.IsNullOrWhiteSpace(name)) continue; // skip blank/total rows

            result.Add(new BacenPixParticipantDto(
                Name: name!,
                Ispb: NormalizeDigits(Get(cells, col["ispb"])),
                Cnpj: NormalizeDigits(Get(cells, col["cnpj"])),
                InstitutionType: Get(cells, col["type"]) ?? string.Empty,
                AuthorizedByBacen: ToBool(Get(cells, col["authorized"])) ?? false,
                SpiParticipationType: Get(cells, col["spi"]),
                PixParticipationType: Get(cells, col["pixType"]),
                PixParticipationMode: Get(cells, col["pixMode"]),
                PaymentInitiation: ToBool(Get(cells, col["initiation"])),
                CashoutServiceFacilitator: ToBool(Get(cells, col["fss"]))));
        }
        return result;
    }

    private static Dictionary<string, int> MapColumns(string[] header)
    {
        int Find(params string[] needles)
        {
            for (var i = 0; i < header.Length; i++)
                if (needles.Any(n => header[i].Contains(n, StringComparison.OrdinalIgnoreCase)))
                    return i;
            return -1;
        }

        var map = new Dictionary<string, int>
        {
            ["name"] = Find("Nome Reduzido"),
            ["ispb"] = Find("ISPB"),
            ["cnpj"] = Find("CNPJ"),
            ["type"] = Find("Tipo de Instituição", "Tipo de Instituicao"),
            ["authorized"] = Find("Autorizada"),
            ["spi"] = Find("Participação no SPI", "Participacao no SPI"),
            ["pixType"] = Find("Tipo de Participação no Pix", "Tipo de Participacao no Pix"),
            ["pixMode"] = Find("Modalidade"),
            ["initiation"] = Find("Iniciação", "Iniciacao"),
            ["fss"] = Find("Facilitador", "FSS")
        };

        var missing = map.Where(kv => kv.Value < 0).Select(kv => kv.Key).ToList();
        if (missing.Contains("name") || missing.Contains("type"))
            throw new MissingColumnsException($"Required columns missing: {string.Join(", ", missing)}");
        return map;
    }

    private static string[] Split(string line) => line.Split(';');

    private static string? Get(string[] cells, int index)
    {
        if (index < 0 || index >= cells.Length) return null;
        var v = cells[index].Trim().Trim('"').Trim();
        return string.IsNullOrWhiteSpace(v) ? null : v;
    }

    private static bool? ToBool(string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) return null;
        var v = value.Trim().ToLowerInvariant();
        if (v is "sim" or "s" or "true") return true;
        if (v is "não" or "nao" or "n" or "false") return false;
        if (v is "n/a" or "na" or "-") return null;
        return null;
    }

    private static string? NormalizeDigits(string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) return null;
        var digits = new string(value.Where(char.IsDigit).ToArray());
        return digits.Length == 0 ? null : digits;
    }
}
