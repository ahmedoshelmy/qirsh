using System.Reflection;

namespace Qirsh.ArchitectureTests;

/// <summary>The Clean Architecture dependency rule, checked on the compiled assemblies.</summary>
public sealed class DependencyRuleTests
{
    private static readonly string[] Contexts = ["Ingestion", "Ledger", "Budgets"];

    public static TheoryData<string> DomainAssemblies => new(Contexts.Select(c => $"Qirsh.{c}.Domain"));

    public static TheoryData<string> ApplicationAssemblies => new(["Qirsh.Ledger.Application", "Qirsh.Budgets.Application"]);

    [Theory]
    [MemberData(nameof(DomainAssemblies))]
    public void Domain_references_nothing_but_the_runtime(string assembly)
    {
        References(assembly).ShouldAllBe(name => name.StartsWith("System", StringComparison.Ordinal) || name == "netstandard");
    }

    [Theory]
    [MemberData(nameof(ApplicationAssemblies))]
    public void Application_does_not_know_infrastructure(string assembly)
    {
        var forbidden = new[] { "Microsoft.EntityFrameworkCore", "MassTransit", "Npgsql", "Grpc", "Microsoft.AspNetCore", ".Infrastructure", ".Api" };

        References(assembly).ShouldAllBe(name => !forbidden.Any(f => name.Contains(f, StringComparison.Ordinal)));
    }

    [Fact]
    public void Bounded_contexts_do_not_reference_each_other()
    {
        foreach (var context in Contexts)
        {
            var others = Contexts.Where(c => c != context).Select(c => $"Qirsh.{c}.").ToArray();
            foreach (var layer in new[] { "Domain", "Infrastructure" })
            {
                References($"Qirsh.{context}.{layer}").ShouldAllBe(name => !others.Any(o => name.StartsWith(o, StringComparison.Ordinal)));
            }
        }
    }

    private static IEnumerable<string> References(string assembly) =>
        Assembly.Load(assembly).GetReferencedAssemblies().Select(a => a.Name ?? "");
}
