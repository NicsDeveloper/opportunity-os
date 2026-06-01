using OpportunityOS.Application.Discovery;
using OpportunityOS.Contracts;

namespace OpportunityOS.Api.Endpoints;

/// <summary>Consulting Radar (P3): discover IT consultancies/software houses, promote to Company.</summary>
public static class ConsultingRadarEndpoints
{
    public static void MapConsultingRadarEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/consulting-radar").WithTags("Consulting Radar");

        group.MapPost("/discover", async (ConsultingDiscoverRequest? req, IConsultingRadarService svc, CancellationToken ct) =>
            Results.Ok(await svc.DiscoverAsync(req ?? new ConsultingDiscoverRequest(null, null), ct)));

        group.MapGet("/candidates", async (int? take, IConsultingRadarService svc, CancellationToken ct) =>
            Results.Ok(await svc.GetCandidatesAsync(take ?? 100, ct)));

        group.MapGet("/candidates/{id:guid}", async (Guid id, IConsultingRadarService svc, CancellationToken ct) =>
        {
            var c = await svc.GetCandidateAsync(id, ct);
            return c is null ? Results.NotFound() : Results.Ok(c);
        });

        group.MapPost("/candidates/{id:guid}/promote-to-company", async (Guid id, IConsultingRadarService svc, CancellationToken ct) =>
            Results.Ok(await svc.PromoteAsync(id, ct)));

        group.MapPost("/promote-batch", async (PromoteConsultingBatchRequest? req, IConsultingRadarService svc, CancellationToken ct) =>
            Results.Ok(await svc.PromoteBatchAsync(req ?? new PromoteConsultingBatchRequest(null, null), ct)));
    }
}
