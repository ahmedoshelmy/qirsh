using System.Diagnostics;
using OpenTelemetry.Trace;

namespace Qirsh.ServiceDefaults;

/// <summary>
/// Root sampler: a client span with no parent is background noise (the outbox delivery service polls Postgres
/// twice a second), so it is dropped; every span inside a request or message trace is kept by the parent-based sampler.
/// </summary>
public sealed class SkipBackgroundClientSpans : Sampler
{
    public override SamplingResult ShouldSample(in SamplingParameters samplingParameters) =>
        new(samplingParameters.Kind == ActivityKind.Client ? SamplingDecision.Drop : SamplingDecision.RecordAndSample);
}
