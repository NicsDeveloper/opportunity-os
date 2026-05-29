namespace OpportunityOS.Domain.Entities;

/// <summary>
/// A financial institution imported from the Banco Central's official "Pix
/// participants" list. This is the raw radar staging area — institutions are
/// later promoted to <see cref="Company"/> with criteria, so the company radar
/// doesn't get flooded with every cooperative.
/// </summary>
public sealed class BacenInstitution
{
    public Guid Id { get; private set; }
    public string Name { get; private set; } = string.Empty;
    public string? Ispb { get; private set; }
    public string? Cnpj { get; private set; }
    public string InstitutionType { get; private set; } = string.Empty;
    public bool AuthorizedByBacen { get; private set; }
    public string? SpiParticipationType { get; private set; }
    public string? PixParticipationType { get; private set; }
    public string? PixParticipationMode { get; private set; }
    public bool? PaymentInitiation { get; private set; }
    public bool? CashoutServiceFacilitator { get; private set; }
    public List<string> Tags { get; private set; } = new();
    public DateTime ImportedAtUtc { get; private set; }
    public DateTime? UpdatedAtUtc { get; private set; }

    private BacenInstitution() { }

    public BacenInstitution(
        string name, string? ispb, string? cnpj, string institutionType, bool authorizedByBacen,
        string? spiParticipationType, string? pixParticipationType, string? pixParticipationMode,
        bool? paymentInitiation, bool? cashoutServiceFacilitator, IEnumerable<string> tags)
    {
        Id = Guid.NewGuid();
        Name = name;
        Ispb = ispb;
        Cnpj = cnpj;
        InstitutionType = institutionType;
        AuthorizedByBacen = authorizedByBacen;
        SpiParticipationType = spiParticipationType;
        PixParticipationType = pixParticipationType;
        PixParticipationMode = pixParticipationMode;
        PaymentInitiation = paymentInitiation;
        CashoutServiceFacilitator = cashoutServiceFacilitator;
        Tags = tags.ToList();
        ImportedAtUtc = DateTime.UtcNow;
    }

    public void UpdateFrom(
        string name, string? ispb, string? cnpj, string institutionType, bool authorizedByBacen,
        string? spiParticipationType, string? pixParticipationType, string? pixParticipationMode,
        bool? paymentInitiation, bool? cashoutServiceFacilitator, IEnumerable<string> tags)
    {
        Name = name;
        Ispb = ispb;
        Cnpj = cnpj;
        InstitutionType = institutionType;
        AuthorizedByBacen = authorizedByBacen;
        SpiParticipationType = spiParticipationType;
        PixParticipationType = pixParticipationType;
        PixParticipationMode = pixParticipationMode;
        PaymentInitiation = paymentInitiation;
        CashoutServiceFacilitator = cashoutServiceFacilitator;
        Tags = tags.ToList();
        UpdatedAtUtc = DateTime.UtcNow;
    }
}
