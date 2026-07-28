using OpportunityOS.Application.Import;

namespace OpportunityOS.IntegrationTests;

/// <summary>
/// Deterministic stand-in for the real PdfPig extractor: returns an anonymized LinkedIn-layout text
/// regardless of the uploaded bytes, so integration tests don't need a real PDF binary fixture.
/// </summary>
public sealed class TestPdfTextExtractor : IPdfTextExtractor
{
    public const string FixtureText =
        """
        Alex Backend Developer
        Desenvolvedor .Net | Node | AWS
        São Paulo, Brasil
        Resumo
        Engenheiro backend com 6 anos de experiência em .NET (C#), microsserviços, AWS, Kafka, RabbitMQ,
        pagamentos, PIX e Open Finance. Uso JavaScript no front quando preciso.
        Experiência
        Itaú Unibanco
        Desenvolvedor .Net Sênior
        janeiro de 2021 - Present (4 anos 2 meses)
        Microsserviços .NET para pagamentos e PIX, Open Finance, AWS Lambda, SQS, DynamoDB.
        Vindi
        Desenvolvedor .Net Pleno
        janeiro de 2019 - dezembro de 2020 (2 anos)
        Plataforma de pagamentos em .NET, SQL Server, RabbitMQ.
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
        """;

    public Task<string> ExtractTextAsync(Stream pdfStream, CancellationToken cancellationToken) =>
        Task.FromResult(FixtureText);
}
