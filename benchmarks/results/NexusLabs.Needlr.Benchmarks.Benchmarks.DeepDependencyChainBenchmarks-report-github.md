```

BenchmarkDotNet v0.14.0, Ubuntu 24.04.4 LTS (Noble Numbat)
Intel Core 7 150U, 1 CPU, 12 logical and 6 physical cores
.NET SDK 10.0.302
  [Host]   : .NET 10.0.10 (10.0.1026.32716), X64 RyuJIT AVX2
  ShortRun : .NET 10.0.10 (10.0.1026.32716), X64 RyuJIT AVX2

Job=ShortRun  IterationCount=5  LaunchCount=1  
WarmupCount=3  

```
| Method                             | Mean     | Error     | StdDev   | Ratio | RatioSD | Rank | Allocated | Alloc Ratio |
|----------------------------------- |---------:|----------:|---------:|------:|--------:|-----:|----------:|------------:|
| ManualDI_ResolveDeepChain          | 16.97 ns | 12.501 ns | 3.247 ns |  1.03 |    0.25 |    2 |         - |          NA |
| Needlr_Reflection_ResolveDeepChain | 11.41 ns |  1.086 ns | 0.168 ns |  0.69 |    0.12 |    1 |         - |          NA |
| Needlr_SourceGen_ResolveDeepChain  | 10.65 ns |  2.568 ns | 0.397 ns |  0.65 |    0.11 |    1 |         - |          NA |
