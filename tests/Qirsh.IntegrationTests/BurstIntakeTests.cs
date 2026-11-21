using System.Diagnostics;
using System.Globalization;
using System.Net.Http.Json;
using Npgsql;
using RabbitMQ.Client;
using RabbitMQ.Client.Exceptions;

namespace Qirsh.IntegrationTests;

/// <summary>
/// Regression test for the stall the observability lab measured (ADR 5): a burst of SMS for the same few cards and
/// merchants made concurrent ledger consumers collide on the same rows. At MassTransit's default RepeatableRead
/// isolation Postgres answered 40001 (could not serialize access), the outbox stalled and messages ended in
/// <c>sms-parsed_error</c>. Every SMS posted here must reach the ledger, quickly, with nothing dead-lettered.
/// </summary>
[Collection(ServicesFixture.Name)]
public sealed class BurstIntakeTests(QirshServices stack)
{
    private const int Burst = 200;
    private static readonly TimeSpan Deadline = TimeSpan.FromSeconds(60);

    private static readonly string[] ErrorQueues =
    [
        "sms-parsed_error", "transaction-recorded_error", "transaction-recategorised_error", "budget-alert-broadcaster_error",
    ];

    // The lab's load-test shapes: three cards, merchants that all match seeded keyword rules (hot rows).
    private static readonly string[] Merchants =
        ["CARREFOUR MAADI", "TALABAT EGYPT", "UBER TRIP", "SPINNEYS ZAYED", "COSTA COFFEE", "SEOUDI MARKET", "IKEA CFC", "VODAFONE RECHARGE"];

    [Fact]
    public async Task A_concurrent_burst_reaches_the_ledger_with_nothing_dead_lettered()
    {
        var before = await CountAsync("ledger.transactions");
        var date = DateTime.UtcNow.ToString("dd/MM/yyyy", CultureInfo.InvariantCulture);
        var client = stack.Ingestion.CreateClient();

        var posts = Enumerable.Range(0, Burst).Select(async i =>
        {
            var response = await client.PostAsJsonAsync("/api/sms", new { text = Sms(i, date) });
            response.EnsureSuccessStatusCode();
        });
        await Task.WhenAll(posts);

        var clock = Stopwatch.StartNew();
        long recorded;
        while ((recorded = await CountAsync("ledger.transactions") - before) < Burst && clock.Elapsed < Deadline)
        {
            await Task.Delay(250);
        }

        recorded.ShouldBe(Burst, $"after {clock.Elapsed.TotalSeconds:0.0} s: {await stack.DescribePipelineAsync()}");
        foreach (var queue in ErrorQueues)
        {
            (await MessagesInAsync(queue)).ShouldBe(0u, queue);
        }

        // The keyword rules' hit counters were incremented by concurrent consumers: none of the increments may be lost.
        (await CountAsync("ledger.merchant_rules", "sum(\"TimesApplied\")")).ShouldBeGreaterThanOrEqualTo(Burst);
    }

    private static string Sms(int i, string date)
    {
        var merchant = Merchants[i % Merchants.Length];
        var amount = string.Create(CultureInfo.InvariantCulture, $"{10 + (i * 37 % 4000)}.{i % 100:00}");
        return (i % 3) switch
        {
            0 => $"CIB: Your card ****4821 was charged EGP {amount} at {merchant} on {date}. Ref burst-{i}",
            1 => $"QNB ALAHLI: Purchase of EGP {amount} at {merchant} using card ending 3307 on {date}. Ref burst-{i}",
            _ => $"Banque Misr: L.E {amount} debited at {merchant} on {date} Ref burst-{i}",
        };
    }

    private async Task<long> CountAsync(string table, string expression = "count(*)")
    {
        await using var connection = new NpgsqlConnection(stack.PostgresConnectionString);
        await connection.OpenAsync();
        await using var command = new NpgsqlCommand($"select coalesce({expression}, 0) from {table}", connection);
        return Convert.ToInt64(await command.ExecuteScalarAsync(), CultureInfo.InvariantCulture);
    }

    /// <summary>Ready messages in a queue; a queue that was never created (nothing failed) counts as empty.</summary>
    private async Task<uint> MessagesInAsync(string queue)
    {
        var factory = new ConnectionFactory { Uri = new Uri(stack.RabbitMqConnectionString) };
        await using var connection = await factory.CreateConnectionAsync();
        await using var channel = await connection.CreateChannelAsync();
        try
        {
            return (await channel.QueueDeclarePassiveAsync(queue)).MessageCount;
        }
        catch (OperationInterruptedException e) when (e.ShutdownReason?.ReplyCode == 404)
        {
            return 0;
        }
    }
}
