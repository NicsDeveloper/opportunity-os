using OpportunityOS.Domain.Entities;
using OpportunityOS.Domain.Enums;

namespace OpportunityOS.Application.Bacen;

/// <summary>
/// Transparent, pure rules for tagging, promotion eligibility and company
/// priority — so the radar is filtered with criteria (not flooded with every
/// cooperative). Unit-tested directly.
/// </summary>
public static class BacenRules
{
    public static List<string> GenerateTags(BacenPixParticipantDto p)
    {
        var tags = new List<string> { "bacen", "pix", "payments" };

        if (p.AuthorizedByBacen) tags.Add("authorized-bcb");
        if (Eq(p.SpiParticipationType, "Direta")) tags.Add("spi-direct");
        if (Eq(p.SpiParticipationType, "Indireta")) tags.Add("spi-indirect");
        if (Eq(p.PixParticipationType, "Obrigatória")) tags.Add("pix-mandatory");
        if (Eq(p.PixParticipationType, "Facultativa")) tags.Add("pix-optional");
        if (p.PaymentInitiation == true) tags.Add("payment-initiator");
        if (p.CashoutServiceFacilitator == true) tags.Add("cashout-facilitator");

        if (Has(p.InstitutionType, "Instituição de Pagamento")) tags.Add("payment-institution");
        if (Has(p.InstitutionType, "Banco")) tags.Add("bank");
        if (Has(p.InstitutionType, "Sociedade de Crédito Direto")) tags.Add("credit-direct");
        if (Has(p.InstitutionType, "Cooperativa de Crédito")) tags.Add("credit-cooperative");
        if (Has(p.InstitutionType, "Corretora") || Has(p.InstitutionType, "DTVM")) tags.Add("broker");

        return tags;
    }

    public static bool IsEligibleForPromotion(BacenPixParticipantDto p)
    {
        if (!p.AuthorizedByBacen) return false;
        if (Has(p.InstitutionType, "Cooperativa de Crédito")) return false;
        if (StartsWithCoopPrefix(p.Name)) return false;

        return Has(p.InstitutionType, "Instituição de Pagamento")
            || Has(p.InstitutionType, "Banco")
            || Has(p.InstitutionType, "Sociedade de Crédito Direto")
            || Has(p.InstitutionType, "Sociedade de Crédito, Financiamento e Investimento");
    }

    public static CompanyPriority SuggestPriority(BacenPixParticipantDto p)
    {
        if (Has(p.InstitutionType, "Cooperativa de Crédito") || !p.AuthorizedByBacen)
            return CompanyPriority.Low;

        var isPaymentOrBank = Has(p.InstitutionType, "Instituição de Pagamento") || Has(p.InstitutionType, "Banco");

        if (p.AuthorizedByBacen && isPaymentOrBank
            && Eq(p.SpiParticipationType, "Direta")
            && Eq(p.PixParticipationMode, "Provedor de Conta Transacional"))
            return CompanyPriority.Strategic;

        if (p.AuthorizedByBacen && (isPaymentOrBank || Has(p.InstitutionType, "Sociedade de Crédito Direto")))
            return CompanyPriority.High;

        if (p.AuthorizedByBacen)
            return CompanyPriority.Medium;

        return CompanyPriority.Low;
    }

    /// <summary>Project a stored institution back to the rule input shape.</summary>
    public static BacenPixParticipantDto ToDto(BacenInstitution i) => new(
        i.Name, i.Ispb, i.Cnpj, i.InstitutionType, i.AuthorizedByBacen,
        i.SpiParticipationType, i.PixParticipationType, i.PixParticipationMode,
        i.PaymentInitiation, i.CashoutServiceFacilitator);

    private static bool Has(string? value, string needle) =>
        value?.Contains(needle, StringComparison.OrdinalIgnoreCase) ?? false;

    private static bool Eq(string? value, string other) =>
        string.Equals(value?.Trim(), other, StringComparison.OrdinalIgnoreCase);

    private static bool StartsWithCoopPrefix(string name)
    {
        var n = name.TrimStart().ToUpperInvariant();
        return n.StartsWith("CCLA") || n.StartsWith("CCC") || n.StartsWith("CC ") || n == "CC";
    }
}
