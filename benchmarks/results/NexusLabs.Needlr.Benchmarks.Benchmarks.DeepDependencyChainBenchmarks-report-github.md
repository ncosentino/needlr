```

BenchmarkDotNet v0.14.0, Ubuntu 24.04.4 LTS (Noble Numbat)
AMD EPYC 7763, 1 CPU, 4 logical and 2 physical cores
.NET SDK 10.0.203
  [Host]   : .NET 10.0.7 (10.0.726.21808), X64 RyuJIT AVX2
  ShortRun : .NET 10.0.7 (10.0.726.21808), X64 RyuJIT AVX2

Job=ShortRun  IterationCount=5  LaunchCount=1  
WarmupCount=3  

```
| Method                             | Mean     | Error    | StdDev   | Ratio | Rank | Allocated | Alloc Ratio |
|----------------------------------- |---------:|---------:|---------:|------:|-----:|----------:|------------:|
| ManualDI_ResolveDeepChain          | 15.41 ns | 0.086 ns | 0.013 ns |  1.00 |    1 |         - |          NA |
| Needlr_Reflection_ResolveDeepChain | 15.47 ns | 0.459 ns | 0.119 ns |  1.00 |    1 |         - |          NA |
| Needlr_SourceGen_ResolveDeepChain  | 15.25 ns | 0.038 ns | 0.010 ns |  0.99 |    1 |         - |          NA |
