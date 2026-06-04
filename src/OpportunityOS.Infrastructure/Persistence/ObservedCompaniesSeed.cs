using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;
using Microsoft.EntityFrameworkCore;
using OpportunityOS.Domain.Entities;
using OpportunityOS.Domain.Enums;

namespace OpportunityOS.Infrastructure.Persistence;

/// <summary>
/// Internal seed (NOT a user feature) that adds companies the user observed manually on
/// LinkedIn into the EXISTING radar (Company), so the current discovery flow
/// (Company → Website Discovery → Career Page → ATS Detection → Job Discovery) can reach them.
/// Idempotent: never duplicates, never overwrites a good WebsiteUrl/CareersUrl, only raises
/// priority and merges tags. No LinkedIn access, no scraping, no applications.
/// </summary>
public static partial class ObservedCompaniesSeed
{
    private enum Cat { Financial, Consulting, Marketplace, Product, Misc }

    private sealed record Seed(string Name, CompanyPriority Priority, Cat Category, string[]? Extra = null);

    // Classification + priority taken from the user's manual LinkedIn observations.
    private static readonly Seed[] Companies =
    {
        // Financial / Strategic
        new("Fin-X", CompanyPriority.Strategic, Cat.Financial),
        new("Blu", CompanyPriority.Strategic, Cat.Financial),
        new("Nubank", CompanyPriority.Strategic, Cat.Financial),
        new("BriteCore", CompanyPriority.Strategic, Cat.Financial, new[] { "insurance", "financial-systems" }),
        // Consulting / Staffing — High
        new("Grupo GBI", CompanyPriority.High, Cat.Consulting, new[] { "dotnet-source" }),
        new("Montreal Oficial", CompanyPriority.High, Cat.Consulting, new[] { "dotnet-source" }),
        new("Claranet Brasil", CompanyPriority.High, Cat.Consulting, new[] { "cloud" }),
        new("Itix", CompanyPriority.High, Cat.Consulting),
        new("Meta", CompanyPriority.High, Cat.Consulting, new[] { "fullstack" }),
        new("Insight Global", CompanyPriority.High, Cat.Consulting),
        new("VDart Digital", CompanyPriority.High, Cat.Consulting, new[] { "dotnet-source" }),
        new("Pride Global", CompanyPriority.High, Cat.Consulting),
        new("Modus Create", CompanyPriority.High, Cat.Consulting),
        new("Avenue Code", CompanyPriority.High, Cat.Consulting, new[] { "fullstack" }),
        new("Nearform", CompanyPriority.High, Cat.Consulting),
        new("LanceSoft Inc.", CompanyPriority.High, Cat.Consulting, new[] { "dotnet-source" }),
        new("Amaris Consulting", CompanyPriority.High, Cat.Consulting),
        // Consulting / Staffing — Medium
        new("A3Data", CompanyPriority.Medium, Cat.Consulting, new[] { "data" }),
        new("Supranet", CompanyPriority.Medium, Cat.Consulting, new[] { "fullstack" }),
        new("M-Tech", CompanyPriority.Medium, Cat.Consulting),
        new("Noorden Group", CompanyPriority.Medium, Cat.Consulting, new[] { "ai" }),
        new("Vertex Agility", CompanyPriority.Medium, Cat.Consulting),
        new("GeorgiaTEK Systems Inc.", CompanyPriority.Medium, Cat.Consulting),
        new("Lazer Technologies", CompanyPriority.Medium, Cat.Consulting, new[] { "fullstack" }),
        new("Curotec", CompanyPriority.Medium, Cat.Consulting, new[] { "fullstack" }),
        new("HeartCentrix Solutions", CompanyPriority.Medium, Cat.Consulting, new[] { "fullstack" }),
        new("Pyramid Consulting Inc.", CompanyPriority.Medium, Cat.Consulting),
        new("Search Wizards", CompanyPriority.Medium, Cat.Consulting, new[] { "recruiting" }),
        new("Signify Technology", CompanyPriority.Medium, Cat.Consulting, new[] { "recruiting" }),
        new("Understanding Solutions", CompanyPriority.Medium, Cat.Consulting),
        new("Velozient", CompanyPriority.Medium, Cat.Consulting),
        new("OTIMIZE Tecnologia em Movimento", CompanyPriority.Medium, Cat.Consulting),
        new("Hays", CompanyPriority.Medium, Cat.Consulting, new[] { "recruiting" }),
        new("Brunel", CompanyPriority.Medium, Cat.Consulting),
        new("Fullinfo", CompanyPriority.Medium, Cat.Consulting),
        new("HighlightTA", CompanyPriority.Medium, Cat.Consulting, new[] { "recruiting" }),
        new("Hyqoo", CompanyPriority.Medium, Cat.Consulting, new[] { "staffing" }),
        // Marketplace
        new("Kake", CompanyPriority.High, Cat.Marketplace, new[] { "dotnet-source", "latam" }),
        new("Proxify", CompanyPriority.High, Cat.Marketplace),
        new("Alignerr", CompanyPriority.Medium, Cat.Marketplace, new[] { "ai" }),
        new("G2i Inc.", CompanyPriority.Medium, Cat.Marketplace, new[] { "ai" }),
        new("micro1", CompanyPriority.Medium, Cat.Marketplace, new[] { "ai" }),
        new("SME Careers", CompanyPriority.Medium, Cat.Marketplace),
        new("Karat", CompanyPriority.Low, Cat.Marketplace, new[] { "interview" }),
        new("Prolific", CompanyPriority.Low, Cat.Marketplace, new[] { "ai" }),
        // Direct employer / product — High
        new("Wave by Bemobi", CompanyPriority.High, Cat.Product),
        new("Conexa", CompanyPriority.High, Cat.Product, new[] { "healthtech" }),
        // Direct employer / product — Medium
        new("InPeace", CompanyPriority.Medium, Cat.Product),
        new("Azify", CompanyPriority.Medium, Cat.Product),
        new("Q4", CompanyPriority.Medium, Cat.Product, new[] { "global" }),
        new("Hyperproof", CompanyPriority.Medium, Cat.Product, new[] { "saas" }),
        new("Causa Certa", CompanyPriority.Medium, Cat.Product),
        new("Newsela", CompanyPriority.Medium, Cat.Product, new[] { "edtech" }),
        new("M3 USA", CompanyPriority.Medium, Cat.Product, new[] { "healthtech" }),
        new("Loadsmart", CompanyPriority.Medium, Cat.Product, new[] { "logistics" }),
        new("OpenAssets", CompanyPriority.Medium, Cat.Product, new[] { "saas" }),
        new("Jusfy", CompanyPriority.Medium, Cat.Product, new[] { "legaltech" }),
        new("AGGRANDIZE", CompanyPriority.Medium, Cat.Product),
        // Low — off-focus / talent pools / noisy (kept on radar, low priority + reason tag)
        new("Môre", CompanyPriority.Low, Cat.Product, new[] { "talent-pool" }),
        new("Seox Inteligência digital para Publishers", CompanyPriority.Low, Cat.Product, new[] { "talent-pool" }),
        new("Servant", CompanyPriority.Low, Cat.Product, new[] { "off-stack" }),
        new("AlphaSights", CompanyPriority.Low, Cat.Consulting, new[] { "off-stack" }),
        new("Alstra Technologies", CompanyPriority.Low, Cat.Product, new[] { "power-platform", "off-stack" }),
        new("Riveron", CompanyPriority.Low, Cat.Product, new[] { "mulesoft", "off-stack" }),
        new("ArcTouch", CompanyPriority.Low, Cat.Consulting, new[] { "mobile", "talent-pool" }),
        new("Techifide Ltd", CompanyPriority.Low, Cat.Product, new[] { "off-stack" }),
        new("Moralis", CompanyPriority.Low, Cat.Product, new[] { "web3", "off-stack" }),
        new("YO HR Consultancy", CompanyPriority.Low, Cat.Consulting, new[] { "recruiting", "noisy-source" }),
        new("Delivery Associates", CompanyPriority.Low, Cat.Product),
        new("Trentini Assessoria Previdenciária", CompanyPriority.Low, Cat.Misc, new[] { "off-stack" }),

        // ── Batch 2 (manual radar seed): real companies/consultancies/products to widen coverage.
        // Dedup is by normalized name, so repeats of entries above are skipped automatically.
        // ── Financial / Strategic — major BR banks, top fintechs and payment infra.
        new("Stone", CompanyPriority.Strategic, Cat.Financial),
        new("Itaú", CompanyPriority.Strategic, Cat.Financial),
        new("Santander", CompanyPriority.Strategic, Cat.Financial),
        new("PicPay", CompanyPriority.Strategic, Cat.Financial),
        new("Cielo", CompanyPriority.Strategic, Cat.Financial),
        new("Banco Inter", CompanyPriority.Strategic, Cat.Financial),
        new("BTG Pactual", CompanyPriority.Strategic, Cat.Financial),
        new("Banco Bradesco", CompanyPriority.Strategic, Cat.Financial),
        new("Banco do Brasil", CompanyPriority.Strategic, Cat.Financial),
        new("Caixa Econômica Federal", CompanyPriority.Strategic, Cat.Financial),
        new("Banco C6", CompanyPriority.Strategic, Cat.Financial),
        new("Adyen", CompanyPriority.Strategic, Cat.Financial, new[] { "global" }),
        new("Asaas", CompanyPriority.Strategic, Cat.Financial),
        new("dLocal", CompanyPriority.Strategic, Cat.Financial, new[] { "global" }),
        new("SumUp", CompanyPriority.Strategic, Cat.Financial, new[] { "global" }),
        new("Pismo", CompanyPriority.Strategic, Cat.Financial),
        new("Belvo", CompanyPriority.Strategic, Cat.Financial, new[] { "open-finance" }),
        new("Celcoin", CompanyPriority.Strategic, Cat.Financial),
        new("RecargaPay", CompanyPriority.Strategic, Cat.Financial),
        // ── Financial / High — banks, payment institutions, fintech infra.
        new("Banco BV", CompanyPriority.High, Cat.Financial),
        new("Paymentology", CompanyPriority.High, Cat.Financial, new[] { "global" }),
        new("BRBCard", CompanyPriority.High, Cat.Financial),
        new("BRB", CompanyPriority.High, Cat.Financial),
        new("Finnet", CompanyPriority.High, Cat.Financial),
        new("Dimensa Tecnologia", CompanyPriority.High, Cat.Financial),
        new("Linx", CompanyPriority.High, Cat.Financial, new[] { "retail-tech" }),
        new("i4pro", CompanyPriority.High, Cat.Financial, new[] { "insurance" }),
        new("Softplan", CompanyPriority.High, Cat.Financial),
        new("NSTECH", CompanyPriority.High, Cat.Financial, new[] { "logistics" }),
        new("Experian", CompanyPriority.High, Cat.Financial, new[] { "global", "credit" }),
        new("Direct Cash", CompanyPriority.High, Cat.Financial),
        new("Banco Original", CompanyPriority.High, Cat.Financial),
        new("Banco Pan", CompanyPriority.High, Cat.Financial),
        new("Banco Sicoob", CompanyPriority.High, Cat.Financial),
        new("Banco XP", CompanyPriority.High, Cat.Financial),
        new("Banco Safra", CompanyPriority.High, Cat.Financial),
        new("Banco Daycoval", CompanyPriority.High, Cat.Financial),
        new("Banco BMG", CompanyPriority.High, Cat.Financial),
        new("Banco BS2", CompanyPriority.High, Cat.Financial),
        new("Banco Bari", CompanyPriority.High, Cat.Financial),
        new("Banco Genial", CompanyPriority.High, Cat.Financial),
        new("Banco Topázio", CompanyPriority.High, Cat.Financial),
        new("Banco Rendimento", CompanyPriority.High, Cat.Financial),
        new("Banco Mercantil", CompanyPriority.High, Cat.Financial),
        new("Banco Pine", CompanyPriority.High, Cat.Financial),
        new("Banco ABC Brasil", CompanyPriority.High, Cat.Financial),
        new("Banco Agibank", CompanyPriority.High, Cat.Financial),
        new("99Pay", CompanyPriority.High, Cat.Financial),
        new("Acesso Soluções de Pagamento", CompanyPriority.High, Cat.Financial),
        new("A55 SCD", CompanyPriority.High, Cat.Financial),
        new("BMP SCD", CompanyPriority.High, Cat.Financial),
        new("BS2 Payments", CompanyPriority.High, Cat.Financial),
        new("Banrisul Pagamentos", CompanyPriority.High, Cat.Financial),
        new("Casas Bahia Pay", CompanyPriority.High, Cat.Financial),
        new("BEES Brasil", CompanyPriority.High, Cat.Financial, new[] { "retail-tech" }),
        new("Bling", CompanyPriority.High, Cat.Financial, new[] { "erp", "saas" }),
        new("Tray", CompanyPriority.High, Cat.Financial, new[] { "ecommerce", "saas" }),
        new("LWSA", CompanyPriority.High, Cat.Financial, new[] { "saas" }),
        new("Gringo", CompanyPriority.High, Cat.Financial),
        new("Zapay", CompanyPriority.High, Cat.Financial),
        new("meutudo", CompanyPriority.High, Cat.Financial),
        new("CondoConta", CompanyPriority.High, Cat.Financial),
        new("Meu Pedágio", CompanyPriority.High, Cat.Financial),
        new("NDD Tech", CompanyPriority.High, Cat.Financial),
        new("Thunes", CompanyPriority.High, Cat.Financial, new[] { "global" }),
        new("Elo", CompanyPriority.High, Cat.Financial),
        new("C&M Software", CompanyPriority.High, Cat.Financial),
        new("Kraken", CompanyPriority.High, Cat.Financial, new[] { "crypto", "global" }),
        new("Coinbase", CompanyPriority.High, Cat.Financial, new[] { "crypto", "global" }),
        new("Binance", CompanyPriority.High, Cat.Financial, new[] { "crypto", "global" }),
        // ── Consulting / Staffing / Software houses — High.
        new("K2 Partnering Solutions", CompanyPriority.High, Cat.Consulting),
        new("BRQ", CompanyPriority.High, Cat.Consulting),
        new("CI&T", CompanyPriority.High, Cat.Consulting),
        new("FCamara", CompanyPriority.High, Cat.Consulting),
        new("Compass UOL", CompanyPriority.High, Cat.Consulting),
        new("Spread Tecnologia", CompanyPriority.High, Cat.Consulting),
        new("Capgemini", CompanyPriority.High, Cat.Consulting, new[] { "global" }),
        new("Accenture Brasil", CompanyPriority.High, Cat.Consulting, new[] { "global" }),
        new("Globant", CompanyPriority.High, Cat.Consulting, new[] { "global" }),
        new("EPAM Systems", CompanyPriority.High, Cat.Consulting, new[] { "global" }),
        new("BairesDev", CompanyPriority.High, Cat.Consulting, new[] { "latam" }),
        new("AgileEngine", CompanyPriority.High, Cat.Consulting, new[] { "latam" }),
        new("FullStack Labs", CompanyPriority.High, Cat.Consulting),
        new("Kyndryl", CompanyPriority.High, Cat.Consulting, new[] { "global" }),
        new("Luxoft", CompanyPriority.High, Cat.Consulting, new[] { "global" }),
        new("CWI Software", CompanyPriority.High, Cat.Consulting),
        new("FOURSYS", CompanyPriority.High, Cat.Consulting),
        new("SysMap Solutions", CompanyPriority.High, Cat.Consulting),
        new("Dexian", CompanyPriority.High, Cat.Consulting),
        new("Provider IT", CompanyPriority.High, Cat.Consulting),
        new("TOPMIND", CompanyPriority.High, Cat.Consulting),
        new("Zup Innovation", CompanyPriority.High, Cat.Consulting),
        new("HCLTech", CompanyPriority.High, Cat.Consulting, new[] { "global" }),
        new("Nava", CompanyPriority.High, Cat.Consulting),
        new("Squadra Digital", CompanyPriority.High, Cat.Consulting),
        new("South System", CompanyPriority.High, Cat.Consulting),
        new("DBC Company", CompanyPriority.High, Cat.Consulting),
        new("Sciensa", CompanyPriority.High, Cat.Consulting),
        new("GFT Technologies", CompanyPriority.High, Cat.Consulting, new[] { "global" }),
        new("NTT DATA", CompanyPriority.High, Cat.Consulting, new[] { "global" }),
        new("NEORIS", CompanyPriority.High, Cat.Consulting, new[] { "global" }),
        new("Invillia", CompanyPriority.High, Cat.Consulting),
        new("Stefanini", CompanyPriority.High, Cat.Consulting),
        new("Radix", CompanyPriority.High, Cat.Consulting),
        new("DB1", CompanyPriority.High, Cat.Consulting),
        new("Zallpy Digital", CompanyPriority.High, Cat.Consulting),
        new("Sensedia", CompanyPriority.High, Cat.Consulting, new[] { "api" }),
        new("MJV", CompanyPriority.High, Cat.Consulting),
        new("Truelogic", CompanyPriority.High, Cat.Consulting, new[] { "latam" }),
        new("Jahnel Group", CompanyPriority.High, Cat.Consulting),
        new("QAT Global", CompanyPriority.High, Cat.Consulting),
        new("T-Systems", CompanyPriority.High, Cat.Consulting, new[] { "global" }),
        new("Engineering Brasil", CompanyPriority.High, Cat.Consulting),
        new("Stack Builders", CompanyPriority.High, Cat.Consulting),
        new("Lanlink", CompanyPriority.High, Cat.Consulting),
        new("Jobsity", CompanyPriority.High, Cat.Consulting, new[] { "latam" }),
        new("Impulso", CompanyPriority.High, Cat.Consulting),
        new("Hitss Brasil", CompanyPriority.High, Cat.Consulting),
        new("Leega Consultoria", CompanyPriority.High, Cat.Consulting),
        new("Viceri", CompanyPriority.High, Cat.Consulting),
        new("Prime Control", CompanyPriority.High, Cat.Consulting, new[] { "qa" }),
        new("Spassu", CompanyPriority.High, Cat.Consulting),
        new("Bravi", CompanyPriority.High, Cat.Consulting),
        new("Sauter", CompanyPriority.High, Cat.Consulting),
        new("Dataside", CompanyPriority.High, Cat.Consulting, new[] { "data" }),
        new("Oowlish", CompanyPriority.High, Cat.Consulting),
        new("Kanda Software", CompanyPriority.High, Cat.Consulting),
        // ── Consulting / Staffing / Software houses — Medium (long tail).
        new("Kaspper", CompanyPriority.Medium, Cat.Consulting),
        new("Innolevels", CompanyPriority.Medium, Cat.Consulting),
        new("Datum", CompanyPriority.Medium, Cat.Consulting),
        new("Deliver IT", CompanyPriority.Medium, Cat.Consulting),
        new("Datainfo", CompanyPriority.Medium, Cat.Consulting),
        new("Sioux Digital", CompanyPriority.Medium, Cat.Consulting),
        new("SoftDesign", CompanyPriority.Medium, Cat.Consulting),
        new("Pitang", CompanyPriority.Medium, Cat.Consulting),
        new("JCal Consultoria", CompanyPriority.Medium, Cat.Consulting),
        new("Develcode", CompanyPriority.Medium, Cat.Consulting),
        new("Aliare", CompanyPriority.Medium, Cat.Consulting, new[] { "agtech" }),
        new("RITS", CompanyPriority.Medium, Cat.Consulting),
        new("Prax Tecnologia", CompanyPriority.Medium, Cat.Consulting),
        new("Cedro Sistemas", CompanyPriority.Medium, Cat.Consulting),
        new("Base2 Tecnologia", CompanyPriority.Medium, Cat.Consulting),
        new("Jaya Tech", CompanyPriority.Medium, Cat.Consulting),
        new("Perform", CompanyPriority.Medium, Cat.Consulting),
        new("Addvisor", CompanyPriority.Medium, Cat.Consulting),
        new("Sedona Digital", CompanyPriority.Medium, Cat.Consulting),
        new("Bluelight Consulting", CompanyPriority.Medium, Cat.Consulting),
        new("Quatto Tecnologia", CompanyPriority.Medium, Cat.Consulting),
        new("Fóton Informática", CompanyPriority.Medium, Cat.Consulting),
        new("AMcom", CompanyPriority.Medium, Cat.Consulting),
        new("IT Labs", CompanyPriority.Medium, Cat.Consulting),
        new("Verity", CompanyPriority.Medium, Cat.Consulting),
        new("Gateware", CompanyPriority.Medium, Cat.Consulting),
        new("InMeta", CompanyPriority.Medium, Cat.Consulting),
        new("Acadia", CompanyPriority.Medium, Cat.Consulting),
        new("SIP Soluções", CompanyPriority.Medium, Cat.Consulting),
        new("Calriz", CompanyPriority.Medium, Cat.Consulting),
        new("Diagonal", CompanyPriority.Medium, Cat.Consulting),
        new("Flatiron Software", CompanyPriority.Medium, Cat.Consulting),
        new("Cadho", CompanyPriority.Medium, Cat.Consulting),
        new("ClasseTech", CompanyPriority.Medium, Cat.Consulting),
        new("Nexus Consulting", CompanyPriority.Medium, Cat.Consulting),
        new("SCS", CompanyPriority.Medium, Cat.Consulting),
        new("GX2", CompanyPriority.Medium, Cat.Consulting),
        new("JCM Group", CompanyPriority.Medium, Cat.Consulting),
        new("AE Digital", CompanyPriority.Medium, Cat.Consulting),
        new("CmdScale", CompanyPriority.Medium, Cat.Consulting),
        new("Trafilea", CompanyPriority.Medium, Cat.Consulting, new[] { "global" }),
        new("MultiBase", CompanyPriority.Medium, Cat.Consulting),
        new("Kambô Tecnologia", CompanyPriority.Medium, Cat.Consulting),
        new("Tecsa Group", CompanyPriority.Medium, Cat.Consulting),
        new("Bracta Tecnologia", CompanyPriority.Medium, Cat.Consulting),
        new("BIX Tecnologia", CompanyPriority.Medium, Cat.Consulting, new[] { "data" }),
        new("Capitani Group", CompanyPriority.Medium, Cat.Consulting),
        new("B05 Sistemas", CompanyPriority.Medium, Cat.Consulting),
        new("Peak One Dev", CompanyPriority.Medium, Cat.Consulting),
        new("Perseus", CompanyPriority.Medium, Cat.Consulting),
        new("Aloud", CompanyPriority.Medium, Cat.Consulting),
        new("Raidiam", CompanyPriority.Medium, Cat.Consulting, new[] { "open-finance" }),
        new("SAAM Auditoria", CompanyPriority.Medium, Cat.Consulting),
        new("Finch Soluções", CompanyPriority.Medium, Cat.Consulting),
        new("Web-Engenharia", CompanyPriority.Medium, Cat.Consulting),
        new("Delta Soluções Inteligentes", CompanyPriority.Medium, Cat.Consulting),
        new("Join Creative Tech", CompanyPriority.Medium, Cat.Consulting),
        new("CIGAM Software", CompanyPriority.Medium, Cat.Consulting, new[] { "erp" }),
        new("Techno Software", CompanyPriority.Medium, Cat.Consulting),
        new("Ibrowse", CompanyPriority.Medium, Cat.Consulting),
        new("INDT Innovation", CompanyPriority.Medium, Cat.Consulting),
        new("Pantheon", CompanyPriority.Medium, Cat.Consulting),
        new("QualityMinds", CompanyPriority.Medium, Cat.Consulting, new[] { "qa" }),
        new("TheBuildCode", CompanyPriority.Medium, Cat.Consulting),
        new("Thera Consulting", CompanyPriority.Medium, Cat.Consulting),
        new("IT Global Services", CompanyPriority.Medium, Cat.Consulting),
        new("InHouse Market", CompanyPriority.Medium, Cat.Consulting),
        new("eRecht24", CompanyPriority.Medium, Cat.Consulting, new[] { "dach" }),
        new("SHAPE DACH", CompanyPriority.Medium, Cat.Consulting, new[] { "dach" }),
        new("STI GmbH", CompanyPriority.Medium, Cat.Consulting, new[] { "dach" }),
        new("isento GmbH", CompanyPriority.Medium, Cat.Consulting, new[] { "dach" }),
        new("E-Solutions", CompanyPriority.Medium, Cat.Consulting),
        new("ACTalent", CompanyPriority.Medium, Cat.Consulting, new[] { "staffing" }),
        new("INSPYR Global", CompanyPriority.Medium, Cat.Consulting, new[] { "staffing" }),
        new("nCube", CompanyPriority.Medium, Cat.Consulting),
        new("Runtalent", CompanyPriority.Medium, Cat.Consulting, new[] { "staffing" }),
        new("Placing-Me", CompanyPriority.Medium, Cat.Consulting, new[] { "recruiting" }),
        new("conquestone", CompanyPriority.Medium, Cat.Consulting),
        new("ITeam", CompanyPriority.Medium, Cat.Consulting),
        new("Grupo Taking", CompanyPriority.Medium, Cat.Consulting),
        new("extractta", CompanyPriority.Medium, Cat.Consulting),
        new("EVT", CompanyPriority.Medium, Cat.Consulting),
        new("infobase", CompanyPriority.Medium, Cat.Consulting),
        new("tiviati", CompanyPriority.Medium, Cat.Consulting),
        new("hyonpar", CompanyPriority.Medium, Cat.Consulting),
        new("korp", CompanyPriority.Medium, Cat.Consulting),
        new("lerian", CompanyPriority.Medium, Cat.Consulting),
        new("sharkit", CompanyPriority.Medium, Cat.Consulting),
        new("upd8", CompanyPriority.Medium, Cat.Consulting),
        new("PR&M", CompanyPriority.Medium, Cat.Consulting),
        new("Brivia", CompanyPriority.Medium, Cat.Consulting),
        new("Grupo Data", CompanyPriority.Medium, Cat.Consulting),
        new("Dentsu", CompanyPriority.Medium, Cat.Consulting, new[] { "global" }),
        new("ARES Consulting", CompanyPriority.Medium, Cat.Consulting),
        new("think about IT", CompanyPriority.Medium, Cat.Consulting, new[] { "dach" }),
        new("Tech Tactix", CompanyPriority.Medium, Cat.Consulting),
        new("Nexure", CompanyPriority.Medium, Cat.Consulting),
        new("Solvedex", CompanyPriority.Medium, Cat.Consulting),
        new("Scopic", CompanyPriority.Medium, Cat.Consulting, new[] { "global" }),
        new("RELQ Technologies", CompanyPriority.Medium, Cat.Consulting),
        new("alphacoders", CompanyPriority.Medium, Cat.Consulting),
        new("Sidestream", CompanyPriority.Medium, Cat.Consulting, new[] { "dach" }),
        new("SAPCON", CompanyPriority.Medium, Cat.Consulting, new[] { "sap" }),
        new("Sintel", CompanyPriority.Medium, Cat.Consulting),
        new("Aquarius", CompanyPriority.Medium, Cat.Consulting),
        new("AI Númera", CompanyPriority.Medium, Cat.Consulting, new[] { "data" }),
        new("Zyte", CompanyPriority.Medium, Cat.Consulting, new[] { "data" }),
        new("Pabum", CompanyPriority.Medium, Cat.Consulting),
        new("Fenix Tecnologia", CompanyPriority.Medium, Cat.Consulting),
        new("NucleoGov", CompanyPriority.Medium, Cat.Consulting, new[] { "govtech" }),
        new("Nooxit", CompanyPriority.Medium, Cat.Consulting),
        new("Seed Technology Solutions", CompanyPriority.Medium, Cat.Consulting),
        new("MSA Recursos Humanos", CompanyPriority.Medium, Cat.Consulting, new[] { "recruiting" }),
        new("Swell IT Solutions", CompanyPriority.Medium, Cat.Consulting),
        new("Wayon Global", CompanyPriority.Medium, Cat.Consulting),
        new("4Solution Group", CompanyPriority.Medium, Cat.Consulting),
        new("INDI Staffing", CompanyPriority.Medium, Cat.Consulting, new[] { "staffing" }),
        new("Supero Outsourcing", CompanyPriority.Medium, Cat.Consulting),
        new("Mirante Tecnologia", CompanyPriority.Medium, Cat.Consulting),
        new("Hiring Machine", CompanyPriority.Medium, Cat.Consulting, new[] { "recruiting" }),
        new("Akvelon", CompanyPriority.Medium, Cat.Consulting, new[] { "global" }),
        new("TechRX Recruiting", CompanyPriority.Medium, Cat.Consulting, new[] { "recruiting" }),
        new("Objective", CompanyPriority.Medium, Cat.Consulting),
        new("Quik Hire Staffing", CompanyPriority.Low, Cat.Consulting, new[] { "staffing", "needs-validation" }),
        // ── Product / Direct employers — High.
        new("VTEX", CompanyPriority.High, Cat.Product, new[] { "ecommerce" }),
        new("Wellhub", CompanyPriority.High, Cat.Product),
        new("Globo", CompanyPriority.High, Cat.Product),
        new("Localiza", CompanyPriority.High, Cat.Product),
        new("Grupo Boticário", CompanyPriority.High, Cat.Product),
        new("RD Station", CompanyPriority.High, Cat.Product, new[] { "saas" }),
        new("Omie", CompanyPriority.High, Cat.Product, new[] { "erp", "saas" }),
        new("Afya", CompanyPriority.High, Cat.Product, new[] { "healthtech" }),
        new("Mottu", CompanyPriority.High, Cat.Product),
        new("QuintoAndar", CompanyPriority.High, Cat.Product, new[] { "proptech" }),
        new("Grupo Casas Bahia", CompanyPriority.High, Cat.Product, new[] { "retail" }),
        new("TOTVS", CompanyPriority.High, Cat.Product, new[] { "erp" }),
        new("GitLab", CompanyPriority.High, Cat.Product, new[] { "global" }),
        new("DoorDash", CompanyPriority.High, Cat.Product, new[] { "global" }),
        new("TRACTIAN", CompanyPriority.High, Cat.Product, new[] { "iot" }),
        new("Deel", CompanyPriority.High, Cat.Product, new[] { "global" }),
        new("Chainlink Labs", CompanyPriority.High, Cat.Product, new[] { "web3", "global" }),
        new("Sysdig", CompanyPriority.High, Cat.Product, new[] { "global" }),
        new("NinjaOne", CompanyPriority.High, Cat.Product, new[] { "global" }),
        new("Hand Talk", CompanyPriority.High, Cat.Product, new[] { "accessibility" }),
        new("Sólides Tecnologia", CompanyPriority.High, Cat.Product, new[] { "hrtech" }),
        new("Pipefy", CompanyPriority.High, Cat.Product, new[] { "saas" }),
        new("Smart Fit", CompanyPriority.High, Cat.Product),
        new("Loft", CompanyPriority.High, Cat.Product, new[] { "proptech" }),
        new("MadeiraMadeira", CompanyPriority.High, Cat.Product, new[] { "ecommerce" }),
        new("Motorola Solutions", CompanyPriority.High, Cat.Product, new[] { "global" }),
        new("Trustly", CompanyPriority.High, Cat.Product, new[] { "fintech", "global" }),
        new("Grupo OLX", CompanyPriority.High, Cat.Product),
        new("Databricks", CompanyPriority.High, Cat.Product, new[] { "data", "global" }),
        new("Red Hat", CompanyPriority.High, Cat.Product, new[] { "global" }),
        new("Amazon", CompanyPriority.High, Cat.Product, new[] { "global" }),
        new("Netflix", CompanyPriority.High, Cat.Product, new[] { "global" }),
        new("Sinch", CompanyPriority.High, Cat.Product, new[] { "global" }),
        new("Ambev Tech", CompanyPriority.High, Cat.Product),
        new("Brisanet", CompanyPriority.High, Cat.Product, new[] { "telecom" }),
        new("SiDi", CompanyPriority.High, Cat.Product),
        new("Instituto Eldorado", CompanyPriority.High, Cat.Product),
        new("SONDA", CompanyPriority.High, Cat.Product, new[] { "latam" }),
        new("Mindbody", CompanyPriority.High, Cat.Product, new[] { "global" }),
        new("Cresta", CompanyPriority.High, Cat.Product, new[] { "ai", "global" }),
        new("Netskope", CompanyPriority.High, Cat.Product, new[] { "global" }),
        new("Affirm", CompanyPriority.High, Cat.Product, new[] { "fintech", "global" }),
        new("Tripadvisor", CompanyPriority.High, Cat.Product, new[] { "global" }),
        new("TRM Labs", CompanyPriority.High, Cat.Product, new[] { "web3", "global" }),
        new("YDUQS", CompanyPriority.High, Cat.Product, new[] { "edtech" }),
        new("Cogna", CompanyPriority.High, Cat.Product, new[] { "edtech" }),
        new("Valid", CompanyPriority.High, Cat.Product),
        new("MV", CompanyPriority.High, Cat.Product, new[] { "healthtech" }),
        new("Wolt", CompanyPriority.High, Cat.Product, new[] { "global" }),
        // ── Product / Direct employers — Medium.
        new("Clicksign", CompanyPriority.Medium, Cat.Product, new[] { "saas" }),
        new("WEX", CompanyPriority.Medium, Cat.Product, new[] { "fintech", "global" }),
        new("ADP", CompanyPriority.Medium, Cat.Product, new[] { "global" }),
        new("Teachable", CompanyPriority.Medium, Cat.Product, new[] { "edtech", "global" }),
        new("Fivetran", CompanyPriority.Medium, Cat.Product, new[] { "data", "global" }),
        new("Whirlpool", CompanyPriority.Medium, Cat.Product, new[] { "global" }),
        new("Halliburton", CompanyPriority.Medium, Cat.Product, new[] { "global" }),
        new("Catawiki", CompanyPriority.Medium, Cat.Product, new[] { "global" }),
        new("Cencosud", CompanyPriority.Medium, Cat.Product, new[] { "retail", "latam" }),
        new("Greenn", CompanyPriority.Medium, Cat.Product, new[] { "saas" }),
        new("Fotop", CompanyPriority.Medium, Cat.Product),
        new("Carbigdata", CompanyPriority.Medium, Cat.Product, new[] { "data" }),
        new("MOTOR Ai", CompanyPriority.Medium, Cat.Product, new[] { "ai" }),
        new("Evision", CompanyPriority.Medium, Cat.Product),
        new("Pling Solutions", CompanyPriority.Medium, Cat.Product),
        new("Engine", CompanyPriority.Medium, Cat.Product),
        new("Overfly", CompanyPriority.Medium, Cat.Product),
        new("Escalada", CompanyPriority.Medium, Cat.Product),
        new("Engravida", CompanyPriority.Medium, Cat.Product),
        new("IlarCo", CompanyPriority.Medium, Cat.Product),
        new("Blue Orange Digital", CompanyPriority.Medium, Cat.Product, new[] { "data", "global" }),
        new("IPmedia", CompanyPriority.Medium, Cat.Product),
        new("Swapcard", CompanyPriority.Medium, Cat.Product, new[] { "global" }),
        new("Axonius", CompanyPriority.Medium, Cat.Product, new[] { "global" }),
        new("Ruby Labs", CompanyPriority.Medium, Cat.Product, new[] { "global" }),
        new("Smart System", CompanyPriority.Medium, Cat.Product),
        new("Speechify", CompanyPriority.Medium, Cat.Product, new[] { "global" }),
        new("IRPD", CompanyPriority.Medium, Cat.Product),
        new("Fostr", CompanyPriority.Medium, Cat.Product),
        new("RS Pneus", CompanyPriority.Medium, Cat.Product),
        new("Athelas", CompanyPriority.Medium, Cat.Product, new[] { "healthtech", "global" }),
        new("Melhores Destinos", CompanyPriority.Medium, Cat.Product, new[] { "travel" }),
        new("Grupo CM", CompanyPriority.Medium, Cat.Product),
        new("Dev.Pro", CompanyPriority.Medium, Cat.Product, new[] { "global" }),
        new("Web Travel Solutions", CompanyPriority.Medium, Cat.Product, new[] { "travel" }),
        new("BWTECH", CompanyPriority.Medium, Cat.Product),
        new("Spun Mídia", CompanyPriority.Medium, Cat.Product),
        new("Konatus", CompanyPriority.Medium, Cat.Product),
        new("Worldly", CompanyPriority.Medium, Cat.Product, new[] { "global" }),
        new("Agencia Open", CompanyPriority.Medium, Cat.Product),
        new("Simplecheck", CompanyPriority.Medium, Cat.Product),
        new("Cint", CompanyPriority.Medium, Cat.Product, new[] { "global" }),
        new("GUIARE", CompanyPriority.Medium, Cat.Product),
        new("Weecom", CompanyPriority.Medium, Cat.Product),
        new("Concert Technologies", CompanyPriority.Medium, Cat.Product),
        new("Dandy", CompanyPriority.Medium, Cat.Product, new[] { "global" }),
        new("Tecnologia Única", CompanyPriority.Medium, Cat.Product),
        new("Topocart", CompanyPriority.Medium, Cat.Product),
        new("Panrotas", CompanyPriority.Medium, Cat.Product, new[] { "travel" }),
        new("Bemax", CompanyPriority.Medium, Cat.Product),
        new("Lello Condomínios", CompanyPriority.Medium, Cat.Product, new[] { "proptech" }),
        new("Rethink by Framework", CompanyPriority.Medium, Cat.Product),
        new("Hysr Tech", CompanyPriority.Medium, Cat.Product),
        new("99x Brazil", CompanyPriority.Medium, Cat.Product),
        new("Superhuman", CompanyPriority.Medium, Cat.Product, new[] { "global" }),
        new("Ewave", CompanyPriority.Medium, Cat.Product),
        new("LOGAME", CompanyPriority.Medium, Cat.Product, new[] { "gaming" }),
        new("OpenVPN", CompanyPriority.Medium, Cat.Product, new[] { "global" }),
        new("Nutrient", CompanyPriority.Medium, Cat.Product, new[] { "global" }),
        new("CommandLink", CompanyPriority.Medium, Cat.Product, new[] { "global" }),
        new("Apaleo", CompanyPriority.Medium, Cat.Product, new[] { "global" }),
        new("Caspar Health", CompanyPriority.Medium, Cat.Product, new[] { "healthtech", "dach" }),
        new("LawnStarter", CompanyPriority.Medium, Cat.Product, new[] { "global" }),
        new("Pay Theory", CompanyPriority.Medium, Cat.Product, new[] { "fintech", "global" }),
        new("Avantos AI", CompanyPriority.Medium, Cat.Product, new[] { "ai" }),
        new("MLabs", CompanyPriority.Medium, Cat.Product, new[] { "saas" }),
        new("XALT", CompanyPriority.Medium, Cat.Product, new[] { "dach" }),
        new("1Password", CompanyPriority.Medium, Cat.Product, new[] { "global" }),
        new("Omnora", CompanyPriority.Medium, Cat.Product),
        new("Mobi7", CompanyPriority.Medium, Cat.Product),
        new("Dimep Sistemas", CompanyPriority.Medium, Cat.Product),
        new("Beltis", CompanyPriority.Medium, Cat.Product),
        new("Look For Me", CompanyPriority.Medium, Cat.Product),
        new("Bemobi", CompanyPriority.Medium, Cat.Product),
        new("Xsolla", CompanyPriority.Medium, Cat.Product, new[] { "gaming", "global" }),
        new("Gartner", CompanyPriority.Medium, Cat.Product, new[] { "global", "needs-validation" }),
        new("Cogna Educação", CompanyPriority.Medium, Cat.Product, new[] { "edtech" }),
        // ── Remote marketplaces / talent networks — Medium, noisy-source (validate before promoting).
        new("Crossing Hurdles", CompanyPriority.Medium, Cat.Marketplace, new[] { "needs-validation" }),
        new("Workana", CompanyPriority.Medium, Cat.Marketplace, new[] { "freelance" }),
        new("TalentX", CompanyPriority.Medium, Cat.Marketplace),
        new("Remote", CompanyPriority.Medium, Cat.Marketplace, new[] { "eor", "global" }),
        new("Hired", CompanyPriority.Medium, Cat.Marketplace, new[] { "needs-validation" }),
        new("Make IT", CompanyPriority.Medium, Cat.Marketplace, new[] { "needs-validation" }),
        new("Rede Talentos", CompanyPriority.Medium, Cat.Marketplace, new[] { "talent-pool", "needs-validation" }),
        // ── Low / validate first (job-board-ish platforms, off-stack, talent pools).
        new("Jobgether", CompanyPriority.Low, Cat.Marketplace, new[] { "needs-validation", "do-not-promote" }),
        new("Jobs Ai", CompanyPriority.Low, Cat.Marketplace, new[] { "needs-validation", "do-not-promote" }),
        new("Hire Feed", CompanyPriority.Low, Cat.Marketplace, new[] { "needs-validation", "do-not-promote" }),
        new("LowCode Agency", CompanyPriority.Low, Cat.Consulting, new[] { "low-code", "needs-validation" }),
        new("Bikeleasing-Service", CompanyPriority.Low, Cat.Product, new[] { "dach", "stack-mismatch" }),
        new("UNDP", CompanyPriority.Low, Cat.Product, new[] { "ngo", "needs-validation" }),
    };

    // Job boards, aggregators, recruiters and personal profiles that must NEVER be a hiring Company.
    // Not created here; if one already exists in the DB it is demoted to a do-not-promote source.
    private static readonly string[] Denylist =
    {
        "Code Vagas", "JobJá", "Dev Life", "Netvagas", "Vagas PJ", "LinkedIn Jobs",
        "SimplyHired", "Indeed", "Glassdoor", "Remotejobs", "Jobbol",
        "Samira Santos", "Hilda Barbosa", "Jeff A.", "Luciane V.", "Pedro Henrique", "Leila Werlich",
    };

    private static readonly string[] GlobalTags = { "observed-linkedin", "manual-radar-seed" };

    private static string[] BaseTags(Cat c) => c switch
    {
        Cat.Financial => new[] { "financial", "fintech", "banking", "payments", "pix", "dotnet-priority", "remote-source" },
        Cat.Consulting => new[] { "consulting", "staffing", "outsourcing", "remote-source", "dotnet-source" },
        Cat.Marketplace => new[] { "remote-marketplace", "talent-network", "contractor", "remote-source", "noisy-source" },
        Cat.Product => new[] { "product-company", "direct-employer", "remote-source", "software-engineering" },
        _ => new[] { "remote-source" },
    };

    private static string? Industry(Cat c) => c switch
    {
        Cat.Financial => "Fintech / Financial",
        Cat.Consulting => "IT Consulting / Staffing",
        Cat.Marketplace => "Remote Talent Marketplace",
        Cat.Product => "Software / Product",
        _ => null,
    };

    public sealed record SeedSummary(
        int Received, int Created, int Updated, int Skipped,
        int Strategic, int High, int Medium, int Low,
        int NeedWebsite, int NeedAts, List<string> Warnings);

    public static async Task<SeedSummary> RunAsync(OpportunityOsDbContext db, CancellationToken ct = default)
    {
        var run = ExecutionRun.Start("ObservedCompaniesSeed");
        await db.ExecutionRuns.AddAsync(run, ct);

        var existing = await db.Companies.ToListAsync(ct);
        var byNorm = existing
            .GroupBy(c => Normalize(c.Name))
            .ToDictionary(g => g.Key, g => g.First());

        int created = 0, updated = 0, skipped = 0, needWebsite = 0, needAts = 0;
        var seen = new HashSet<string>(StringComparer.Ordinal);
        var warnings = new List<string>();
        var byPriority = new Dictionary<CompanyPriority, int>();

        foreach (var seed in Companies)
        {
            var norm = Normalize(seed.Name);
            if (!seen.Add(norm)) { skipped++; continue; } // intra-list dup

            var tags = GlobalTags
                .Concat(BaseTags(seed.Category))
                .Concat(seed.Extra ?? Array.Empty<string>())
                .ToList();

            Company company;
            if (byNorm.TryGetValue(norm, out var found))
            {
                company = found;
                foreach (var t in tags) company.AddTag(t);
                company.RaisePriorityTo(seed.Priority); // never downgrade
                updated++;
                run.RecordSuccess();
            }
            else
            {
                company = new Company(seed.Name, websiteUrl: null, careersUrl: null, linkedInUrl: null,
                    industry: Industry(seed.Category), country: null, seed.Priority, CompanySource.SearchDiscovery, tags);
                await db.Companies.AddAsync(company, ct);
                byNorm[norm] = company;
                created++;
                run.RecordSuccess();
            }

            // Mark for the existing discovery flow only when data is missing (don't overwrite).
            if (string.IsNullOrWhiteSpace(company.WebsiteUrl)) { company.AddTag("needs-website-discovery"); needWebsite++; }
            if (string.IsNullOrWhiteSpace(company.CareersUrl)) { company.AddTag("needs-ats-detection"); needAts++; }

            byPriority[company.Priority] = byPriority.GetValueOrDefault(company.Priority) + 1;
        }

        // Denylist: never create job boards/aggregators/personal profiles; if one already exists,
        // demote it to a low-priority do-not-promote source (don't delete — keep it for source dedup).
        var denyNorms = Denylist.Select(Normalize).ToHashSet(StringComparer.Ordinal);
        var demoted = 0;
        foreach (var c in existing.Where(c => denyNorms.Contains(Normalize(c.Name))))
        {
            c.MarkSourceOnly();
            demoted++;
        }
        if (demoted > 0) warnings.Add($"denylisted existentes rebaixados para source-only: {demoted}");

        run.Complete();
        await db.SaveChangesAsync(ct);

        return new SeedSummary(
            Companies.Length, created, updated, skipped,
            byPriority.GetValueOrDefault(CompanyPriority.Strategic),
            byPriority.GetValueOrDefault(CompanyPriority.High),
            byPriority.GetValueOrDefault(CompanyPriority.Medium),
            byPriority.GetValueOrDefault(CompanyPriority.Low),
            needWebsite, needAts, warnings);
    }

    /// <summary>Normalize for dedup: lowercase, strip accents/punctuation and common suffixes
    /// (Inc, Ltd, LTDA, S.A., Oficial, Brasil).</summary>
    public static string Normalize(string name)
    {
        var lowered = (name ?? string.Empty).Trim().ToLowerInvariant();
        var noAccent = new string(lowered.Normalize(NormalizationForm.FormD)
            .Where(c => CharUnicodeInfo.GetUnicodeCategory(c) != UnicodeCategory.NonSpacingMark).ToArray());
        var cleaned = NonAlnumRegex().Replace(noAccent, " ");
        var words = cleaned.Split(' ', StringSplitOptions.RemoveEmptyEntries)
            .Where(w => w is not ("inc" or "ltd" or "ltda" or "sa" or "oficial" or "brasil"))
            .ToArray();
        return string.Join(" ", words);
    }

    [GeneratedRegex(@"[^a-z0-9]")]
    private static partial Regex NonAlnumRegex();
}
