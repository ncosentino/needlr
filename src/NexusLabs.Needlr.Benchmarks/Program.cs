using BenchmarkDotNet.Running;

namespace NexusLabs.Needlr.Benchmarks;

public class Program
{
    public static void Main(string[] args)
    {
        // Run all benchmarks or filter via command line args
        BenchmarkSwitcher.FromAssembly(typeof(Program).Assembly).Run(args);
    }
}
