using OpportunityOS.Application.Discovery;
using OpportunityOS.Contracts;

namespace OpportunityOS.Api.Endpoints;

/// <summary>Firehose — massive candidate discovery (campaigns, quick/aggressive search).</summary>
public static class DiscoveryEndpoints
{
    public static void MapDiscoveryEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/discovery").WithTags("Discovery (Firehose)");

        group.MapPost("/campaigns", async (CreateCampaignRequest req, IFirehoseService svc, CancellationToken ct) =>
        {
            if (string.IsNullOrWhiteSpace(req.Name)) return Results.BadRequest("Name is required.");
            var created = await svc.CreateCampaignAsync(req, ct);
            return Results.Created($"/api/discovery/campaigns/{created.Id}", created);
        });

        group.MapGet("/campaigns", async (IFirehoseService svc, CancellationToken ct) =>
            Results.Ok(await svc.GetCampaignsAsync(ct)));

        group.MapGet("/campaigns/{id:guid}", async (Guid id, IFirehoseService svc, CancellationToken ct) =>
        {
            var c = await svc.GetCampaignAsync(id, ct);
            return c is null ? Results.NotFound() : Results.Ok(c);
        });

        group.MapPost("/campaigns/{id:guid}/run", async (
            Guid id, AggressiveSearchRequest? req, IFirehoseService svc, CancellationToken ct) =>
        {
            var payload = (req ?? new AggressiveSearchRequest(null, null, null, null, null)) with { CampaignId = id };
            try { return Results.Ok(await svc.AggressiveSearchAsync(payload, ct)); }
            catch (InvalidOperationException ex) { return Results.NotFound(ex.Message); }
        });

        group.MapPost("/quick-search", async (QuickSearchRequest req, IFirehoseService svc, CancellationToken ct) =>
        {
            if (string.IsNullOrWhiteSpace(req.Query)) return Results.BadRequest("Query is required.");
            return Results.Ok(await svc.QuickSearchAsync(req, ct));
        });

        group.MapPost("/aggressive-search", async (AggressiveSearchRequest req, IFirehoseService svc, CancellationToken ct) =>
        {
            try { return Results.Ok(await svc.AggressiveSearchAsync(req, ct)); }
            catch (InvalidOperationException ex) { return Results.NotFound(ex.Message); }
        });

        // Raw volume (so the user can see everything the Firehose collected).
        group.MapGet("/raw-candidates", async (int? take, IFirehoseService svc, CancellationToken ct) =>
            Results.Ok(await svc.GetRawCandidatesAsync(take ?? 100, ct)));
    }
}
