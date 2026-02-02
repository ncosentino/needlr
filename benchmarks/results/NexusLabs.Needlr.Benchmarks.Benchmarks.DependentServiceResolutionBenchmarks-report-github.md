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
| ManualDI_ResolveDependent          | 15.96 ns | 1.775 ns | 0.461 ns |  1.00 |    0.04 |    1 |         - |          NA |
| Needlr_Reflection_ResolveDependent | 14.72 ns | 0.661 ns | 0.172 ns |  0.92 |    0.03 |    1 |         - |          NA |
| Needlr_SourceGen_ResolveDependent  | 15.31 ns | 0.341 ns | 0.089 ns |  0.96 |    0.03 |    1 |         - |          NA |
