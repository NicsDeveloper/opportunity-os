using OpportunityOS.Application.Bacen;

namespace OpportunityOS.IntegrationTests;

/// <summary>Deterministic Bacen CSV provider for integration tests (no network).</summary>
public sealed class TestBacenCsvProvider : IBacenPixParticipantsCsvProvider
{
    public Task<IReadOnlyCollection<BacenPixParticipantDto>> GetParticipantsAsync(CancellationToken ct)
    {
        IReadOnlyCollection<BacenPixParticipantDto> rows = new[]
        {
            new BacenPixParticipantDto("99PAY IP S.A.", "24313102", "24313102000125",
                "Instituição de Pagamento", true, "Direta", "Facultativa", "Provedor de Conta Transacional", true, false),
            new BacenPixParticipantDto("BANCO BTG PACTUAL S.A.", "30306294", "30306294000145",
                "Banco Múltiplo", true, "Direta", "Obrigatória", "Provedor de Conta Transacional", false, false),
            new BacenPixParticipantDto("CECRED COOP", "12345678", "12345678000100",
                "Cooperativa de Crédito", true, "Indireta", "Obrigatória", "Provedor de Conta Transacional", false, false),
            new BacenPixParticipantDto("FOO TEST IP", "87654321", null,
                "Instituição de Pagamento", false, null, null, null, null, null)
        };
        return Task.FromResult(rows);
    }
}
