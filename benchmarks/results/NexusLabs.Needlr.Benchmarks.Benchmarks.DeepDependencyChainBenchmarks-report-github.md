```

BenchmarkDotNet v0.14.0, Ubuntu 24.04.4 LTS (Noble Numbat)
AMD EPYC 7763, 1 CPU, 4 logical and 2 physical cores
.NET SDK 10.0.302
  [Host]   : .NET 10.0.11 (10.0.1126.37416), X64 RyuJIT AVX2
  ShortRun : .NET 10.0.11 (10.0.1126.37416), X64 RyuJIT AVX2

Job=ShortRun  IterationCount=5  LaunchCount=1  
WarmupCount=3  

```
| Method                             | Mean     | Error    | StdDev   | Ratio | Rank | Allocated | Alloc Ratio |
|----------------------------------- |---------:|---------:|---------:|------:|-----:|----------:|------------:|
| ManualDI_ResolveDeepChain          | 16.25 ns | 0.681 ns | 0.177 ns |  1.00 |    1 |         - |          NA |
| Needlr_Reflection_ResolveDeepChain | 15.53 ns | 0.682 ns | 0.105 ns |  0.96 |    1 |         - |          NA |
| Needlr_SourceGen_ResolveDeepChain  | 15.25 ns | 0.499 ns | 0.077 ns |  0.94 |    1 |         - |          NA |
