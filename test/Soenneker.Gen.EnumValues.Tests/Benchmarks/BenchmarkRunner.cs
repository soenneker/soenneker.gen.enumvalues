namespace Soenneker.Gen.EnumValues.Tests.Benchmarks;

public class BenchmarkRunner : BenchmarkTest
{
    public BenchmarkRunner() : base()
    {
    }

    //[LocalOnly]
    public async ValueTask EnumValuesListBenchmark()
    {
        Summary summary = BenchmarkDotNet.Running.BenchmarkRunner.Run<EnumValuesListBenchmark>(DefaultConf);

        await summary.OutputSummaryToLog();
    }

   // [LocalOnly]
    public async ValueTask TryFromNameBenchmark()
    {
        Summary summary = BenchmarkDotNet.Running.BenchmarkRunner.Run<TryFromNameBenchmark>(DefaultConf);

        await summary.OutputSummaryToLog();
    }

  //  [LocalOnly]
    public async ValueTask TryFromValueBenchmark()
    {
        Summary summary = BenchmarkDotNet.Running.BenchmarkRunner.Run<TryFromValueBenchmark>(DefaultConf);

        await summary.OutputSummaryToLog();
    }

    //[LocalOnly]
    public async ValueTask SerializationBenchmark()
    {
        Summary summary = BenchmarkDotNet.Running.BenchmarkRunner.Run<SerializationBenchmark>(DefaultConf);

        await summary.OutputSummaryToLog();
    }
}


