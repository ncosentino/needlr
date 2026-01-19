using BenchmarkDotNet.Configs;
using BenchmarkDotNet.Jobs;
using BenchmarkDotNet.Diagnosers;

namespace NexusLabs.Needlr.Benchmarks;

/// <summary>
/// Benchmark configuration for Needlr performance tests.
/// Uses ShortRun for quick iteration with memory analysis.
/// </summary>
public class BenchmarkConfig : ManualConfig
{
    public BenchmarkConfig()
    {
        // ShortRun for quick iteration
        AddJob(Job.ShortRun
            .WithWarmupCount(3)
            .WithIterationCount(5));

        // Memory analysis
        AddDiagnoser(MemoryDiagnoser.Default);

        // Useful columns
        AddColumn(BenchmarkDotNet.Columns.RankColumn.Arabic);
    }
}
