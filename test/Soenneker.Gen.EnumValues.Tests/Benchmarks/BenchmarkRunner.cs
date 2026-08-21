using System;
using System.Threading.Tasks;
using BenchmarkDotNet.Reports;
using Soenneker.Benchmarking.Extensions.Summary;
using Soenneker.Tests.Benchmark;

namespace Soenneker.Gen.EnumValues.Tests.Benchmarks;

public class BenchmarkRunner : BenchmarkTest
{
    public BenchmarkRunner() : base()
    {
        Environment.SetEnvironmentVariable("RunBenchmarks", "true");
    }

    [Test]
    [Skip("manual")]
    public async ValueTask EnumValuesListBenchmark()
    {
        Summary summary = BenchmarkDotNet.Running.BenchmarkRunner.Run<EnumValuesListBenchmark>(DefaultConf);

        await summary.OutputSummaryToLog();
    }

    [Test]
    [Skip("manual")]
    public async ValueTask TryFromNameBenchmark()
    {
        Summary summary = BenchmarkDotNet.Running.BenchmarkRunner.Run<TryFromNameRoutingBenchmark>(DefaultConf);

        await summary.OutputSummaryToLog();
    }

    [Test]
    [Skip("manual")]
    public async ValueTask TryFromValueBenchmark()
    {
        Summary summary = BenchmarkDotNet.Running.BenchmarkRunner.Run<TryFromValueBenchmark>(DefaultConf);

        await summary.OutputSummaryToLog();
    }

    [Test]
    [Skip("manual")]
    public async ValueTask SerializationBenchmark()
    {
        Summary summary = BenchmarkDotNet.Running.BenchmarkRunner.Run<SerializationBenchmark>(DefaultConf);

        await summary.OutputSummaryToLog();
    }

    [Test]
    [Skip("manual")]
    public async ValueTask SerializationDispatchBenchmark()
    {
        Summary summary = BenchmarkDotNet.Running.BenchmarkRunner.Run<global::Soenneker.Gen.EnumValues.Tests.Benchmarks.SerializationDispatchBenchmark>(DefaultConf);

        await summary.OutputSummaryToLog();
    }
}
