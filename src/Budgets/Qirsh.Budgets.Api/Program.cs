using Qirsh.Budgets.Api;
using Qirsh.Budgets.Application;
using Qirsh.Budgets.Infrastructure;
using Qirsh.ServiceDefaults;

var builder = WebApplication.CreateBuilder(args);

builder.AddServiceDefaults();
builder.AddServiceDatabase<BudgetsDbContext>(BudgetsDbContext.Schema);
builder.AddQirshMessaging<BudgetsDbContext>(x =>
{
    x.AddConsumer<TransactionRecordedConsumer>();
    x.AddConsumer<TransactionRecategorisedConsumer>();
    x.AddConsumer<BudgetAlertBroadcaster>();
});
var ledgerGrpc = builder.Configuration["Services:LedgerGrpc"] ?? throw new InvalidOperationException("Services:LedgerGrpc is not set.");
builder.Services.AddBudgetsApplication().AddBudgetsInfrastructure(new Uri(ledgerGrpc));
builder.Services.AddSignalR();

var app = builder.Build();

app.MapDefaultEndpoints();
app.MapBudgetEndpoints();
app.MapHub<AlertsHub>(AlertsHub.Path);

await app.MigrateDatabaseAsync<BudgetsDbContext>();
await app.RunAsync();
