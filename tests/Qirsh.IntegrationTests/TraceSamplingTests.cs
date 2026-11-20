using System.Diagnostics;
using OpenTelemetry.Trace;
using Qirsh.ServiceDefaults;

namespace Qirsh.IntegrationTests;

/// <summary>Root client spans (outbox polling) are dropped; requests and message handling are kept.</summary>
public sealed class TraceSamplingTests
{
    private static SamplingDecision Decide(ActivityKind kind) =>
        new SkipBackgroundClientSpans().ShouldSample(new SamplingParameters(default, ActivityTraceId.CreateRandom(), "span", kind)).Decision;

    [Theory]
    [InlineData(ActivityKind.Server, SamplingDecision.RecordAndSample)]
    [InlineData(ActivityKind.Consumer, SamplingDecision.RecordAndSample)]
    [InlineData(ActivityKind.Internal, SamplingDecision.RecordAndSample)]
    [InlineData(ActivityKind.Client, SamplingDecision.Drop)]
    public void Only_root_client_spans_are_dropped(ActivityKind kind, SamplingDecision expected)
    {
        Decide(kind).ShouldBe(expected);
    }
}
