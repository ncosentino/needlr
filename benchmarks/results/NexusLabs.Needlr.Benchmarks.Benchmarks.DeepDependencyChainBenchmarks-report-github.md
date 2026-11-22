```

BenchmarkDotNet v0.14.0, Ubuntu 24.04.5 LTS (Noble Numbat)
AMD EPYC 9V74, 1 CPU, 4 logical and 2 physical cores
.NET SDK 10.0.404
  [Host]   : .NET 10.0.15 (10.0.1526.53008), X64 RyuJIT AVX-512F+CD+BW+DQ+VL+VBMI
  ShortRun : .NET 10.0.15 (10.0.1526.53008), X64 RyuJIT AVX-512F+CD+BW+DQ+VL+VBMI

Job=ShortRun  IterationCount=5  LaunchCount=1  
WarmupCount=3  

```
| Method                             | Mean     | Error    | StdDev   | Ratio | RatioSD | Rank | Allocated | Alloc Ratio |
|----------------------------------- |---------:|---------:|---------:|------:|--------:|-----:|----------:|------------:|
| ManualDI_ResolveDeepChain          | 12.10 ns | 0.051 ns | 0.013 ns |  1.00 |    0.00 |    1 |         - |          NA |
| Needlr_Reflection_ResolveDeepChain | 15.05 ns | 0.448 ns | 0.116 ns |  1.24 |    0.01 |    2 |         - |          NA |
| Needlr_SourceGen_ResolveDeepChain  | 14.87 ns | 3.697 ns | 0.572 ns |  1.23 |    0.04 |    2 |         - |          NA |
