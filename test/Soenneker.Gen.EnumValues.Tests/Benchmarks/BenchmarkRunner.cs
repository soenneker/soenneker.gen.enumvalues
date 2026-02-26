using BenchmarkDotNet.Reports;
using Soenneker.Benchmarking.Extensions.Summary;
using Soenneker.Tests.Benchmark;
using System.Threading.Tasks;
using Xunit;

namespace Soenneker.Gen.EnumValues.Tests.Benchmarks;

public class BenchmarkRunner : BenchmarkTest
{
    public BenchmarkRunner(ITestOutputHelper outputHelper) : base(outputHelper)
    {
    }

 //   [Fact]
    public async ValueTask EnumValuesListBenchmark()
    {
        Summary summary = BenchmarkDotNet.Running.BenchmarkRunner.Run<EnumValuesListBenchmark>(DefaultConf);

        await summary.OutputSummaryToLog(OutputHelper, CancellationToken);
    }

    // [Fact]
    public async ValueTask TryFromNameBenchmark()
    {
        Summary summary = BenchmarkDotNet.Running.BenchmarkRunner.Run<TryFromNameBenchmark>(DefaultConf);

        await summary.OutputSummaryToLog(OutputHelper, CancellationToken);
    }

    //  [Fact]
    public async ValueTask TryFromValueBenchmark()
    {
        Summary summary = BenchmarkDotNet.Running.BenchmarkRunner.Run<TryFromValueBenchmark>(DefaultConf);

        await summary.OutputSummaryToLog(OutputHelper, CancellationToken);
    }

    // [Fact]
    public async ValueTask SerializationBenchmark()
    {
        Summary summary = BenchmarkDotNet.Running.BenchmarkRunner.Run<SerializationBenchmark>(DefaultConf);

        await summary.OutputSummaryToLog(OutputHelper, CancellationToken);
    }
}