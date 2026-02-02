```

BenchmarkDotNet v0.14.0, Ubuntu 24.04.3 LTS (Noble Numbat)
AMD EPYC 7763, 1 CPU, 4 logical and 2 physical cores
.NET SDK 10.0.102
  [Host]   : .NET 10.0.2 (10.0.225.61305), X64 RyuJIT AVX2
  ShortRun : .NET 10.0.2 (10.0.225.61305), X64 RyuJIT AVX2

Job=ShortRun  IterationCount=5  LaunchCount=1  
WarmupCount=3  

```
| Method                             | Mean     | Error    | StdDev   | Ratio | RatioSD | Rank | Allocated | Alloc Ratio |
|----------------------------------- |---------:|---------:|---------:|------:|--------:|-----:|----------:|------------:|
| ManualDI_ResolveDeepChain          | 15.31 ns | 3.081 ns | 0.800 ns |  1.00 |    0.07 |    1 |         - |          NA |
| Needlr_Reflection_ResolveDeepChain | 15.02 ns | 0.453 ns | 0.118 ns |  0.98 |    0.05 |    1 |         - |          NA |
| Needlr_SourceGen_ResolveDeepChain  | 15.77 ns | 2.088 ns | 0.542 ns |  1.03 |    0.06 |    1 |         - |          NA |
