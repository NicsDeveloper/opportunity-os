using Microsoft.EntityFrameworkCore;
using OpportunityOS.Application.Discovery;
using OpportunityOS.Contracts;
using OpportunityOS.Infrastructure.Persistence;

namespace OpportunityOS.Api.Endpoints;

/// <summary>
/// Admin operations panel (policy "Admin" = is_admin claim). Lets an operator see the radar's
/// state and trigger an on-demand, time-boxed sweep of all registered companies. The recurring
/// continuous capture runs in the Worker; this is the manual override.
/// </summary>
public static class AdminEndpoints
{
    public static void MapAdminEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/admin").WithTags("Admin").RequireAuthorization("Admin");

        // Snapshot for the admin tab: counts, the recurring-job schedule, and recent runs.
        group.MapGet("/overview", async (OpportunityOsDbContext db, IConfiguration config, CancellationToken ct) =>
        {
            var companies = await db.Companies.CountAsync(ct);
            var scannable = await db.Companies.CountAsync(c => c.CareersUrl != null && c.CareersUrl != "", ct);
            var jobs = await db.JobPostings.CountAsync(ct);
            var matches = await db.LatestOpportunityMatches.CountAsync(ct);

            var runs = await db.ExecutionRuns
                .OrderByDescending(r => r.StartedAtUtc).Take(10)
                .Select(r => new {
                    r.Id, r.RunType, status = r.Status.ToString(), r.StartedAtUtc, r.FinishedAtUtc,
                    r.ItemsProcessed, r.ItemsSucceeded, r.ItemsFailed })
                .ToListAsync(ct);

            // The Worker reads these crons; surfaced here so the admin knows the continuous cadence.
            var recurring = new[]
            {
                new { name = "continuous-discovery", cron = config["Jobs:ContinuousDiscoveryCron"] ?? "*/15 * * * *", desc = "Varredura de todas as empresas" },
                new { name = "discover-jobs",        cron = config["Jobs:DailyDiscoveryCron"]    ?? "0 8 * * *",     desc = "Varredura diária completa" },
                new { name = "search-jobs",          cron = config["Jobs:SearchCron"]            ?? "0 */3 * * *",   desc = "Busca por palavra-chave" },
                new { name = "firehose-sweep",       cron = config["Jobs:FirehoseCron"]          ?? "0 */4 * * *",   desc = "Descoberta ampla (firehose)" },
                new { name = "validate-links",       cron = config["Jobs:ValidateLinksCron"]     ?? "30 */6 * * *",  desc = "Expira vagas mortas" },
            };

            return Results.Ok(new { companies, scannable, jobs, matches, runs, recurring });
        });

        // Trigger an on-demand sweep of all companies, bounded by maxCompanies and/or maxDurationSeconds.
        // Runs in the background (own DI scope + timeout CTS) so the request returns immediately.
        group.MapPost("/sweep", (
            AdminSweepRequest? req, IServiceScopeFactory scopes, ILoggerFactory loggerFactory) =>
        {
            var maxCompanies = req?.MaxCompanies is { } mc && mc > 0 ? mc : (int?)null;
            var seconds = Math.Clamp(req?.MaxDurationSeconds ?? 120, 5, 1800);
            var logger = loggerFactory.CreateLogger("AdminSweep");

            _ = Task.Run(async () =>
            {
                using var scope = scopes.CreateScope();
                var discovery = scope.ServiceProvider.GetRequiredService<IJobDiscoveryService>();
                using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(seconds));
                try
                {
                    var r = await discovery.DiscoverAllAsync(maxCompanies, cts.Token);
                    logger.LogInformation(
                        "Admin sweep finished {Status}: companies={Companies} new={New} updated={Updated} errors={Errors}",
                        r.Status, r.CompaniesProcessed, r.JobsDiscovered, r.JobsUpdated, r.Errors);
                }
                catch (Exception ex) { logger.LogError(ex, "Admin sweep failed"); }
            });

            return Results.Accepted(value: new { started = true, maxCompanies, maxDurationSeconds = seconds });
        });
    }
}
