using Hangfire;
using Hangfire.PostgreSql;
using OpportunityOS.Infrastructure;
using OpportunityOS.Worker;
using OpportunityOS.Worker.Jobs;

var builder = Host.CreateApplicationBuilder(args);

builder.Services.AddInfrastructure(builder.Configuration);

var connectionString = builder.Configuration.GetConnectionString("DefaultConnection")
    ?? "Host=localhost;Port=5432;Database=opportunity_os;Username=postgres;Password=postgres";

builder.Services.AddHangfire(cfg => cfg
    .SetDataCompatibilityLevel(CompatibilityLevel.Version_180)
    .UseSimpleAssemblyNameTypeSerializer()
    .UseRecommendedSerializerSettings()
    .UsePostgreSqlStorage(opts => opts.UseNpgsqlConnection(connectionString)));

builder.Services.AddHangfireServer();

builder.Services.AddScoped<DiscoverJobsJob>();
builder.Services.AddScoped<SearchJobsJob>();
builder.Services.AddScoped<SendDailyDigestJob>();
builder.Services.AddHostedService<RecurringJobScheduler>();

var host = builder.Build();
host.Run();
