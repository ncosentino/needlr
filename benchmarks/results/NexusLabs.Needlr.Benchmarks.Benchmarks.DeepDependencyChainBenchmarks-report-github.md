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
| ManualDI_ResolveDeepChain          | 15.44 ns | 0.712 ns | 0.110 ns |  1.00 |    0.01 |    1 |         - |          NA |
| Needlr_Reflection_ResolveDeepChain | 15.04 ns | 1.054 ns | 0.274 ns |  0.97 |    0.02 |    1 |         - |          NA |
| Needlr_SourceGen_ResolveDeepChain  | 15.31 ns | 0.334 ns | 0.052 ns |  0.99 |    0.01 |    1 |         - |          NA |
