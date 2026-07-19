```

BenchmarkDotNet v0.14.0, Ubuntu 24.04.4 LTS (Noble Numbat)
AMD EPYC 9V74, 1 CPU, 4 logical and 2 physical cores
.NET SDK 10.0.302
  [Host]   : .NET 10.0.10 (10.0.1026.32716), X64 RyuJIT AVX2
  ShortRun : .NET 10.0.10 (10.0.1026.32716), X64 RyuJIT AVX2

Job=ShortRun  IterationCount=5  LaunchCount=1  
WarmupCount=3  

```
| Method                             | Mean     | Error    | StdDev   | Ratio | Rank | Allocated | Alloc Ratio |
|----------------------------------- |---------:|---------:|---------:|------:|-----:|----------:|------------:|
| ManualDI_ResolveDeepChain          | 18.57 ns | 0.166 ns | 0.026 ns |  1.00 |    1 |         - |          NA |
| Needlr_Reflection_ResolveDeepChain | 19.41 ns | 0.656 ns | 0.101 ns |  1.05 |    1 |         - |          NA |
| Needlr_SourceGen_ResolveDeepChain  | 19.05 ns | 0.756 ns | 0.117 ns |  1.03 |    1 |         - |          NA |
