using Qirsh.Ledger.Api;
using Qirsh.Ledger.Application;
using Qirsh.Ledger.Infrastructure;
using Qirsh.ServiceDefaults;

var builder = WebApplication.CreateBuilder(args);

builder.AddServiceDefaults();
builder.AddServiceDatabase<LedgerDbContext>(LedgerDbContext.Schema);
builder.AddQirshMessaging<LedgerDbContext>(x => x.AddConsumer<SmsParsedConsumer>());
builder.Services.AddLedgerApplication().AddLedgerInfrastructure();
builder.Services.AddGrpc();

var app = builder.Build();

app.MapDefaultEndpoints();
app.MapLedgerEndpoints();
app.MapGrpcService<LedgerQueryGrpcService>();

await app.MigrateDatabaseAsync<LedgerDbContext>();
await app.RunAsync();
