using System.Globalization;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Http.Connections;
using Microsoft.AspNetCore.SignalR.Client;
using Qirsh.Contracts;

namespace Qirsh.IntegrationTests;

[Collection(ServicesFixture.Name)]
public sealed class SmsToBudgetFlowTests(QirshServices stack)
{
    private static readonly TimeSpan Patience = TimeSpan.FromSeconds(30);
    private static readonly DateOnly FirstOfMonth = new(DateTime.UtcNow.Year, DateTime.UtcNow.Month, 1);
    private static readonly string Month = FirstOfMonth.ToString("yyyy-MM", CultureInfo.InvariantCulture);
    private static readonly string SmsDate = FirstOfMonth.ToString("dd/MM/yyyy", CultureInfo.InvariantCulture);

    private sealed record IntakeResponse(int Recorded, int Duplicates, int Skipped);

    private sealed record TransactionRow(Guid Id, string Merchant, string Category, decimal Amount);

    private sealed record BudgetRow(Guid Id, string Category, decimal Spent, decimal Limit, int AlertedThreshold);

    private sealed record CreateBudgetResponse(BudgetRow Budget, bool Backfilled);

    [Fact]
    public async Task All_services_report_ready_with_database_and_broker()
    {
        foreach (var client in new[] { stack.Ingestion.CreateClient(), stack.Ledger.CreateClient(), stack.Budgets.CreateClient() })
        {
            var response = await client.GetAsync(new Uri("/health/ready", UriKind.Relative));
            var body = await response.Content.ReadAsStringAsync();
            response.IsSuccessStatusCode.ShouldBeTrue(body);
        }
    }

    [Fact]
    public async Task Sms_becomes_a_transaction_updates_the_budget_and_pushes_a_live_alert()
    {
        var budgets = stack.Budgets.CreateClient();
        (await budgets.PostAsJsonAsync("/api/budgets", new { category = "Groceries", limit = 1000m, month = Month })).EnsureSuccessStatusCode();

        var alert = new TaskCompletionSource<BudgetThresholdCrossed>(TaskCreationOptions.RunContinuationsAsynchronously);
        await using var hub = new HubConnectionBuilder()
            .WithUrl(new Uri(stack.Budgets.Server.BaseAddress, "/hubs/alerts"), o =>
            {
                o.HttpMessageHandlerFactory = _ => stack.Budgets.Server.CreateHandler();
                o.Transports = HttpTransportType.LongPolling;
            })
            .Build();
        hub.On<BudgetThresholdCrossed>("budgetAlert", a => alert.TrySetResult(a));
        await hub.StartAsync();

        var intake = await PostSms($"CIB: Your card ****4821 was charged EGP 850.00 at CARREFOUR MAADI on {SmsDate}. Avail Bal EGP 3,150.00");
        intake.Recorded.ShouldBe(1);

        var tx = await Eventually(async () => (await Transactions()).SingleOrDefault(t => t.Merchant == "CARREFOUR MAADI"));
        tx.Category.ShouldBe("Groceries");
        tx.Amount.ShouldBe(850m);

        var budget = await Eventually(async () => (await Budgets()).SingleOrDefault(b => b.Category == "Groceries" && b.Spent == 850m));
        budget.AlertedThreshold.ShouldBe(80);

        var pushed = await alert.Task.WaitAsync(Patience);
        pushed.Category.ShouldBe("Groceries");
        pushed.ThresholdPercent.ShouldBe(80);
    }

    [Fact]
    public async Task The_same_sms_posted_twice_is_one_transaction()
    {
        var sms = $"NBE: EGP 120.00 paid at KIOSK DOKKI using card ending 7781 on {SmsDate}";

        (await PostSms(sms)).Recorded.ShouldBe(1);
        (await PostSms(sms)).Duplicates.ShouldBe(1);

        await Eventually(async () => (await Transactions()).FirstOrDefault(t => t.Merchant == "KIOSK DOKKI"));
        await Task.Delay(TimeSpan.FromSeconds(2));
        (await Transactions()).Count(t => t.Merchant == "KIOSK DOKKI").ShouldBe(1);
    }

    [Fact]
    public async Task A_budget_created_after_spending_is_backfilled_over_grpc_and_not_double_counted()
    {
        (await PostSms($"QNB: Purchase of EGP 300.00 at CAREEM RIDE using card ending 5521 on {SmsDate}")).Recorded.ShouldBe(1);
        await Eventually(async () => (await Transactions()).FirstOrDefault(t => t.Merchant == "CAREEM RIDE"));

        var response = await stack.Budgets.CreateClient().PostAsJsonAsync("/api/budgets", new { category = "Transport", limit = 1000m, month = Month });
        response.EnsureSuccessStatusCode();
        var created = await response.Content.ReadFromJsonAsync<CreateBudgetResponse>();

        created!.Backfilled.ShouldBeTrue();
        created.Budget.Spent.ShouldBe(300m);
        await Task.Delay(TimeSpan.FromSeconds(2));
        (await Budgets()).Single(b => b.Category == "Transport").Spent.ShouldBe(300m);
    }

    [Fact]
    public async Task Correcting_a_category_moves_budget_spend_and_teaches_the_merchant()
    {
        var budgets = stack.Budgets.CreateClient();
        (await budgets.PostAsJsonAsync("/api/budgets", new { category = "Entertainment", limit = 500m, month = Month })).EnsureSuccessStatusCode();
        (await PostSms($"HSBC: EGP 200.00 spent at ZOOM ARCADE on {SmsDate}")).Recorded.ShouldBe(1);
        var first = await Eventually(async () => (await Transactions()).SingleOrDefault(t => t.Merchant == "ZOOM ARCADE"));
        first.Category.ShouldBe("Uncategorised");

        var moved = await stack.Ledger.CreateClient().PutAsJsonAsync($"/api/transactions/{first.Id}/category", new { category = "Entertainment" });
        moved.EnsureSuccessStatusCode();

        await Eventually(async () => (await Budgets()).SingleOrDefault(b => b.Category == "Entertainment" && b.Spent == 200m));
        (await PostSms($"HSBC: EGP 50.00 spent at Zoom  Arcade on {SmsDate}. Avail Bal EGP 900.00")).Recorded.ShouldBe(1);
        var second = await Eventually(async () => (await Transactions()).SingleOrDefault(t => t.Merchant == "Zoom Arcade"));
        second.Category.ShouldBe("Entertainment");
        await Eventually(async () => (await Budgets()).SingleOrDefault(b => b.Category == "Entertainment" && b.Spent == 250m));
    }

    private async Task<IntakeResponse> PostSms(string text)
    {
        var response = await stack.Ingestion.CreateClient().PostAsJsonAsync("/api/sms", new { text });
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<IntakeResponse>())!;
    }

    private async Task<List<TransactionRow>> Transactions() =>
        (await stack.Ledger.CreateClient().GetFromJsonAsync<List<TransactionRow>>($"/api/transactions?month={Month}"))!;

    private async Task<List<BudgetRow>> Budgets() =>
        (await stack.Budgets.CreateClient().GetFromJsonAsync<List<BudgetRow>>($"/api/budgets?month={Month}"))!;

    /// <summary>Polls (bounded) until the asynchronous pipeline produces a value.</summary>
    private async Task<T> Eventually<T>(Func<Task<T?>> probe)
        where T : class
    {
        for (var attempt = 0; attempt < 60; attempt++)
        {
            if (await probe() is { } value)
            {
                return value;
            }

            await Task.Delay(500);
        }

        throw new TimeoutException($"The pipeline did not produce the expected state in 30 s: {await stack.DescribePipelineAsync()}");
    }
}
