```

BenchmarkDotNet v0.14.0, Ubuntu 24.04.3 LTS (Noble Numbat)
AMD EPYC 7763, 1 CPU, 4 logical and 2 physical cores
.NET SDK 10.0.102
  [Host]   : .NET 10.0.2 (10.0.225.61305), X64 RyuJIT AVX2
  ShortRun : .NET 10.0.2 (10.0.225.61305), X64 RyuJIT AVX2

Job=ShortRun  IterationCount=5  LaunchCount=1  
WarmupCount=3  

```
| Method                          | Mean     | Error    | StdDev   | Ratio | RatioSD | Rank | Allocated | Alloc Ratio |
|-------------------------------- |---------:|---------:|---------:|------:|--------:|-----:|----------:|------------:|
| ManualDI_ResolveSimple          | 14.64 ns | 0.344 ns | 0.089 ns |  1.00 |    0.01 |    1 |         - |          NA |
| Needlr_Reflection_ResolveSimple | 15.19 ns | 1.460 ns | 0.379 ns |  1.04 |    0.02 |    1 |         - |          NA |
| Needlr_SourceGen_ResolveSimple  | 15.44 ns | 1.309 ns | 0.340 ns |  1.05 |    0.02 |    1 |         - |          NA |
