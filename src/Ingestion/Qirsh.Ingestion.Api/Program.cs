using FluentValidation;
using Qirsh.Ingestion.Api;
using Qirsh.Ingestion.Infrastructure;
using Qirsh.ServiceDefaults;

await HealthProbe.RunIfRequestedAsync(args);

var builder = WebApplication.CreateBuilder(args);

builder.AddServiceDefaults();
builder.AddServiceDatabase<IngestionDbContext>(IngestionDbContext.Schema);
builder.AddQirshMessaging<IngestionDbContext>();
builder.Services.AddIngestionInfrastructure(builder.Configuration["Qirsh:TimeZone"]);
builder.Services.AddValidatorsFromAssemblyContaining<SmsPasteValidator>();
builder.Services.AddGrpc();

var app = builder.Build();

app.MapDefaultEndpoints();
app.MapSmsEndpoints();
app.MapGrpcService<SmsParserGrpcService>();

await app.MigrateDatabaseAsync<IngestionDbContext>();
await app.SeedDemoDataAsync();
await app.RunAsync();
