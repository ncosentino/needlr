```

BenchmarkDotNet v0.14.0, Ubuntu 24.04.4 LTS (Noble Numbat)
AMD EPYC 7763, 1 CPU, 4 logical and 2 physical cores
.NET SDK 10.0.301
  [Host]   : .NET 10.0.9 (10.0.926.27113), X64 RyuJIT AVX2
  ShortRun : .NET 10.0.9 (10.0.926.27113), X64 RyuJIT AVX2

Job=ShortRun  IterationCount=5  LaunchCount=1  
WarmupCount=3  

```
| Method                             | Mean     | Error    | StdDev   | Ratio | Rank | Allocated | Alloc Ratio |
|----------------------------------- |---------:|---------:|---------:|------:|-----:|----------:|------------:|
| ManualDI_ResolveDeepChain          | 15.62 ns | 0.086 ns | 0.013 ns |  1.00 |    1 |         - |          NA |
| Needlr_Reflection_ResolveDeepChain | 15.35 ns | 0.112 ns | 0.029 ns |  0.98 |    1 |         - |          NA |
| Needlr_SourceGen_ResolveDeepChain  | 15.88 ns | 0.135 ns | 0.021 ns |  1.02 |    1 |         - |          NA |
