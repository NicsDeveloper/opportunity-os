namespace OpportunityOS.UnitTests;

/// <summary>
/// ANONYMIZED LinkedIn-export text in the real structural layout (main column first, then sidebar),
/// matching what PdfPigTextExtractor produces. Fictional identity — never commit real personal data.
/// </summary>
public static class LinkedInPdfFixture
{
    public const string Text =
        """
        Alex Backend Developer
        Desenvolvedor .Net | Node | AWS
        São Paulo, Brasil
        Resumo
        Engenheiro backend com 6 anos de experiência em sistemas distribuídos de alta criticidade, com
        forte atuação em .NET (C#) e arquitetura de microsserviços. Vivência sólida em AWS, mensageria
        (Kafka, RabbitMQ), pagamentos, PIX, Open Finance e banking. Uso JavaScript no front quando preciso.
        Experiência
        Itaú Unibanco
        Desenvolvedor .Net Sênior
        janeiro de 2021 - Present (4 anos 2 meses)
        São Paulo, Brasil
        Microsserviços .NET para pagamentos e PIX, integrações Open Finance, Kafka, AWS Lambda, SQS, DynamoDB.
        Vindi
        Desenvolvedor .Net Pleno
        janeiro de 2019 - dezembro de 2020 (2 anos)
        Plataforma de pagamentos recorrentes em .NET, SQL Server, RabbitMQ.
        Formação acadêmica
        Estácio
        Bacharelado, Sistemas de Informação
        2015 - 2019
        Contato
        alex.backend@example.com
        www.linkedin.com/in/alex-backend-example
        Principais competências
        xUnit
        .NET Core
        RabbitMQ
        Certifications
        AZ-900
        EF SET English B2
        """;

    // A document that is clearly NOT a LinkedIn export.
    public const string NotLinkedIn =
        """
        ACME CORP — Internal Memo
        Quarterly results and budget planning for the finance department.
        Please review the attached spreadsheet before the meeting on Friday.
        """;
}
