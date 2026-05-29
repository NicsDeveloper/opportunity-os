using OpportunityOS.Application.Companies;
using Xunit;

namespace OpportunityOS.UnitTests;

public sealed class CsvCompanyParserTests
{
    [Fact]
    public void Parse_WithHeader_ReadsRows()
    {
        var csv = "name,websiteUrl,careersUrl,industry,country\n" +
                  "Acme Pay,https://acme.com,https://boards.greenhouse.io/acme,Fintech,Brazil\n" +
                  "Beta,,,,";
        var rows = CsvCompanyParser.Parse(csv);

        Assert.Equal(2, rows.Count);
        Assert.Equal("Acme Pay", rows[0].Name);
        Assert.Equal("https://boards.greenhouse.io/acme", rows[0].CareersUrl);
        Assert.Equal("Brazil", rows[0].Country);
        Assert.Equal("Beta", rows[1].Name);
        Assert.Null(rows[1].WebsiteUrl);
    }

    [Fact]
    public void Parse_SkipsRowsWithoutName_AndHandlesQuotedCommas()
    {
        var csv = "\"Gupy, Inc\",https://gupy.io\n,https://noname.com";
        var rows = CsvCompanyParser.Parse(csv);

        var row = Assert.Single(rows);
        Assert.Equal("Gupy, Inc", row.Name);
    }

    [Fact]
    public void Parse_EmptyInput_ReturnsEmpty()
    {
        Assert.Empty(CsvCompanyParser.Parse(""));
    }
}
