using System;
using System.Threading.Tasks;
using BenchmarkDotNet.Reports;
using Soenneker.Benchmarking.Extensions.Summary;
using Soenneker.Tests.Benchmark;
using System.Threading;

namespace Soenneker.Gen.EnumValues.Tests.Benchmarks;

public class BenchmarkRunner : BenchmarkTest
{
    public BenchmarkRunner() : base()
    {
        Environment.SetEnvironmentVariable("RunBenchmarks", "true");
    }

    [Test]
    [Skip("manual")]
    public async ValueTask EnumValuesListBenchmark(CancellationToken cancellationToken)
    {
        Summary summary = BenchmarkDotNet.Running.BenchmarkRunner.Run<EnumValuesListBenchmark>(DefaultConf);

        await summary.OutputSummaryToLog(cancellationToken: cancellationToken);
    }

    [Test]
    [Skip("manual")]
    public async ValueTask TryFromNameBenchmark(CancellationToken cancellationToken)
    {
        Summary summary = BenchmarkDotNet.Running.BenchmarkRunner.Run<TryFromNameRoutingBenchmark>(DefaultConf);

        await summary.OutputSummaryToLog(cancellationToken: cancellationToken);
    }

    [Test]
    [Skip("manual")]
    public async ValueTask TryFromValueBenchmark(CancellationToken cancellationToken)
    {
        Summary summary = BenchmarkDotNet.Running.BenchmarkRunner.Run<TryFromValueBenchmark>(DefaultConf);

        await summary.OutputSummaryToLog(cancellationToken: cancellationToken);
    }

    [Test]
    [Skip("manual")]
    public async ValueTask SerializationBenchmark(CancellationToken cancellationToken)
    {
        Summary summary = BenchmarkDotNet.Running.BenchmarkRunner.Run<SerializationBenchmark>(DefaultConf);

        await summary.OutputSummaryToLog(cancellationToken: cancellationToken);
    }

    [Test]
    [Skip("manual")]
    public async ValueTask SerializationDispatchBenchmark(CancellationToken cancellationToken)
    {
        Summary summary = BenchmarkDotNet.Running.BenchmarkRunner.Run<global::Soenneker.Gen.EnumValues.Tests.Benchmarks.SerializationDispatchBenchmark>(DefaultConf);

        await summary.OutputSummaryToLog(cancellationToken: cancellationToken);
    }
}
