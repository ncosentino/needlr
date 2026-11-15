```

BenchmarkDotNet v0.14.0, Ubuntu 24.04.5 LTS (Noble Numbat)
AMD EPYC 7763, 1 CPU, 4 logical and 2 physical cores
.NET SDK 10.0.404
  [Host]   : .NET 10.0.15 (10.0.1526.53008), X64 RyuJIT AVX2
  ShortRun : .NET 10.0.15 (10.0.1526.53008), X64 RyuJIT AVX2

Job=ShortRun  IterationCount=5  LaunchCount=1  
WarmupCount=3  

```
| Method                             | Mean     | Error    | StdDev   | Ratio | RatioSD | Rank | Allocated | Alloc Ratio |
|----------------------------------- |---------:|---------:|---------:|------:|--------:|-----:|----------:|------------:|
| ManualDI_ResolveDependent          | 15.28 ns | 0.530 ns | 0.082 ns |  1.00 |    0.01 |    1 |         - |          NA |
| Needlr_Reflection_ResolveDependent | 15.84 ns | 2.002 ns | 0.520 ns |  1.04 |    0.03 |    1 |         - |          NA |
| Needlr_SourceGen_ResolveDependent  | 14.74 ns | 0.149 ns | 0.023 ns |  0.96 |    0.00 |    1 |         - |          NA |
