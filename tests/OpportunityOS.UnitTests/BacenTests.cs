using OpportunityOS.Application.Bacen;
using OpportunityOS.Domain.Enums;
using Xunit;

namespace OpportunityOS.UnitTests;

public sealed class BacenTests
{
    // Mirrors the real file: title line, header, then rows with a leading index column.
    private const string Csv =
        "Lista de participantes ativos do Pix\n" +
        " ;Nome Reduzido;ISPB;CNPJ;Tipo de Instituição;Autorizada pelo BCB;Tipo de Participação no SPI;Tipo de Participação no Pix;Modalidade de Participação no Pix;Iniciação de Transação de Pagamento;Facilitador de serviço de Saque e Troco (FSS)\n" +
        "1;99PAY IP S.A.;24313102;24.313.102/0001-25;Instituição de Pagamento;Sim;Direta;Facultativa;Provedor de Conta Transacional;Sim;Não\n" +
        "2;BANCO BTG PACTUAL S.A.;30306294;30.306.294/0001-45;Banco Múltiplo;Sim;Direta;Obrigatória;Provedor de Conta Transacional;Não;Não\n" +
        "3;BANCO INTER;00416968;00.416.968/0001-01;Banco Múltiplo;Sim;Indireta;Obrigatória;Provedor de Conta Transacional;Sim;Sim\n" +
        "4;ASAAS IP S.A.;13935893;13.935.893/0001-09;Instituição de Pagamento;Sim;Indireta;Facultativa;Provedor de Conta Transacional;N/A;Não\n" +
        "5;CECRED COOP;12345678;12.345.678/0001-00;Cooperativa de Crédito;Sim;Indireta;Obrigatória;Provedor de Conta Transacional;Não;Não\n" +
        "6;FOO TEST IP;87654321;;Instituição de Pagamento;Não;;;;;\n" +
        "\n";

    private static IReadOnlyList<BacenPixParticipantDto> Parsed() => BacenCsvParser.Parse(Csv);

    [Fact]
    public void Parse_MapsFields_AndSkipsBlankRows()
    {
        var rows = Parsed();
        Assert.Equal(6, rows.Count);

        var p99 = rows[0];
        Assert.Equal("99PAY IP S.A.", p99.Name);
        Assert.Equal("24313102", p99.Ispb);
        Assert.Equal("24313102000125", p99.Cnpj); // digits only
        Assert.Equal("Instituição de Pagamento", p99.InstitutionType);
        Assert.True(p99.AuthorizedByBacen);
        Assert.Equal("Direta", p99.SpiParticipationType);
        Assert.True(p99.PaymentInitiation);
        Assert.False(p99.CashoutServiceFacilitator);
    }

    [Fact]
    public void Parse_ConvertsSimNaoNa()
    {
        var rows = Parsed();
        Assert.True(rows[1].PaymentInitiation == false);   // BTG: Não
        Assert.Null(rows[3].PaymentInitiation);            // Asaas: N/A -> null
        Assert.Null(rows[5].Cnpj);                         // FOO: empty CNPJ -> null
        Assert.False(rows[5].AuthorizedByBacen);           // FOO: Não
    }

    [Fact]
    public void Parse_Throws_WhenHeaderMissing()
    {
        Assert.Throws<BacenCsvParser.MissingColumnsException>(() => BacenCsvParser.Parse("a;b;c\n1;2;3"));
    }

    [Fact]
    public void Tags_AreGeneratedCorrectly_For99Pay()
    {
        var tags = BacenRules.GenerateTags(Parsed()[0]);
        Assert.Contains("bacen", tags);
        Assert.Contains("pix", tags);
        Assert.Contains("payments", tags);
        Assert.Contains("authorized-bcb", tags);
        Assert.Contains("spi-direct", tags);
        Assert.Contains("pix-optional", tags);
        Assert.Contains("payment-initiator", tags);
        Assert.Contains("payment-institution", tags);
    }

    [Theory]
    [InlineData(0, CompanyPriority.Strategic)] // 99PAY: IP, Direta, Provedor de Conta Transacional
    [InlineData(1, CompanyPriority.Strategic)] // BTG: Banco, Direta, Provedor
    [InlineData(2, CompanyPriority.High)]      // Inter: Banco but Indireta
    [InlineData(3, CompanyPriority.High)]      // Asaas: IP but Indireta
    [InlineData(4, CompanyPriority.Low)]       // Cooperativa
    [InlineData(5, CompanyPriority.Low)]       // não autorizada
    public void Priority_FollowsRules(int row, CompanyPriority expected)
    {
        Assert.Equal(expected, BacenRules.SuggestPriority(Parsed()[row]));
    }

    [Fact]
    public void Eligibility_ExcludesCooperativeAndUnauthorized()
    {
        var rows = Parsed();
        Assert.True(BacenRules.IsEligibleForPromotion(rows[0]));  // 99PAY
        Assert.True(BacenRules.IsEligibleForPromotion(rows[1]));  // BTG
        Assert.False(BacenRules.IsEligibleForPromotion(rows[4])); // Cooperativa
        Assert.False(BacenRules.IsEligibleForPromotion(rows[5])); // não autorizada
    }
}
