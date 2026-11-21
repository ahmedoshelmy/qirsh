using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Http;
using Qirsh.Budgets.Api;
using Qirsh.Ingestion.Api;
using Qirsh.Ledger.Api;
using Testcontainers.PostgreSql;
using Testcontainers.RabbitMq;

namespace Qirsh.IntegrationTests;

/// <summary>
/// The three services in-process (TestServer) against a real Postgres and RabbitMQ in containers: the same
/// outbox, broker and gRPC paths as production, minus Kestrel.
/// </summary>
public sealed class QirshServices : IAsyncLifetime
{
    private readonly PostgreSqlContainer _postgres = new PostgreSqlBuilder("postgres:17-alpine")
        .WithDatabase("qirsh")
        .Build();

    private readonly RabbitMqContainer _rabbit = new RabbitMqBuilder("rabbitmq:4-management-alpine")
        .Build();

    public WebApplicationFactory<SmsPasteValidator> Ingestion { get; private set; } = null!;
    public WebApplicationFactory<LedgerQueryGrpcService> Ledger { get; private set; } = null!;
    public WebApplicationFactory<AlertsHub> Budgets { get; private set; } = null!;

    public string PostgresConnectionString => _postgres.GetConnectionString();

    public string RabbitMqConnectionString => _rabbit.GetConnectionString();

    public async Task InitializeAsync()
    {
        await Task.WhenAll(_postgres.StartAsync(), _rabbit.StartAsync());

        // Started one by one so each service migrates its own schema without racing the others.
        Ledger = new ServiceFactory<LedgerQueryGrpcService>(Settings());
        _ = Ledger.Server;
        Ingestion = new ServiceFactory<SmsPasteValidator>(Settings());
        _ = Ingestion.Server;
        Budgets = new ServiceFactory<AlertsHub>(
            Settings(("Services:LedgerGrpc", Ledger.Server.BaseAddress.ToString())),
            services => services.Configure<HttpClientFactoryOptions>("LedgerQueryClient", o =>
                o.HttpMessageHandlerBuilderActions.Add(b => b.PrimaryHandler = new Http2ResponseHandler(Ledger.Server.CreateHandler()))));
        _ = Budgets.Server;
    }

    public async Task DisposeAsync()
    {
        await Budgets.DisposeAsync();
        await Ingestion.DisposeAsync();
        await Ledger.DisposeAsync();
        await Task.WhenAll(_postgres.DisposeAsync().AsTask(), _rabbit.DisposeAsync().AsTask());
    }

    /// <summary>Row counts along the pipeline, for timeout messages: where did the message stop?</summary>
    public async Task<string> DescribePipelineAsync()
    {
        string[] tables =
        [
            "ingestion.raw_messages", "ingestion.\"OutboxMessage\"", "ingestion.\"OutboxState\"", "ledger.\"InboxState\"", "ledger.transactions",
            "ledger.\"OutboxMessage\"", "budgets.\"InboxState\"", "budgets.applied_transactions", "budgets.\"OutboxMessage\"",
        ];
        await using var connection = new Npgsql.NpgsqlConnection(_postgres.GetConnectionString());
        await connection.OpenAsync();
        var counts = new List<string>();
        foreach (var table in tables)
        {
            await using var command = new Npgsql.NpgsqlCommand($"select count(*) from {table}", connection);
            counts.Add($"{table}={await command.ExecuteScalarAsync()}");
        }

        return string.Join(", ", counts);
    }

    private Dictionary<string, string> Settings(params (string Key, string Value)[] extra)
    {
        var settings = new Dictionary<string, string>
        {
            ["ConnectionStrings:Postgres"] = _postgres.GetConnectionString(),
            ["ConnectionStrings:RabbitMq"] = _rabbit.GetConnectionString(),
            ["Messaging:OutboxQueryDelay"] = "00:00:00.200",
        };
        foreach (var (key, value) in extra)
        {
            settings[key] = value;
        }

        return settings;
    }

    private sealed class ServiceFactory<TEntry>(Dictionary<string, string> settings, Action<IServiceCollection>? configure = null)
        : WebApplicationFactory<TEntry>
        where TEntry : class
    {
        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            builder.UseEnvironment("Testing");
            foreach (var (key, value) in settings)
            {
                builder.UseSetting(key, value);
            }

            if (configure is not null)
            {
                builder.ConfigureTestServices(configure);
            }
        }
    }

    /// <summary>gRPC needs HTTP/2 responses; TestServer answers with the request's version only when asked to.</summary>
    private sealed class Http2ResponseHandler(HttpMessageHandler inner) : DelegatingHandler(inner)
    {
        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            var response = await base.SendAsync(request, cancellationToken);
            response.Version = request.Version;
            return response;
        }
    }
}

[CollectionDefinition(Name)]
public sealed class ServicesFixture : ICollectionFixture<QirshServices>
{
    public const string Name = "services";
}
