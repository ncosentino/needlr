```

BenchmarkDotNet v0.14.0, Ubuntu 24.04.4 LTS (Noble Numbat)
AMD EPYC 7763, 1 CPU, 4 logical and 2 physical cores
.NET SDK 10.0.301
  [Host]   : .NET 10.0.9 (10.0.926.27113), X64 RyuJIT AVX2
  ShortRun : .NET 10.0.9 (10.0.926.27113), X64 RyuJIT AVX2

Job=ShortRun  IterationCount=5  LaunchCount=1  
WarmupCount=3  

```
| Method                          | Mean     | Error    | StdDev   | Ratio | Rank | Allocated | Alloc Ratio |
|-------------------------------- |---------:|---------:|---------:|------:|-----:|----------:|------------:|
| ManualDI_ResolveSimple          | 15.23 ns | 0.095 ns | 0.025 ns |  1.00 |    1 |         - |          NA |
| Needlr_Reflection_ResolveSimple | 15.15 ns | 0.316 ns | 0.049 ns |  0.99 |    1 |         - |          NA |
| Needlr_SourceGen_ResolveSimple  | 15.05 ns | 0.236 ns | 0.037 ns |  0.99 |    1 |         - |          NA |
