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
| ManualDI_ResolveDependent          | 15.52 ns | 0.364 ns | 0.056 ns |  1.00 |    1 |         - |          NA |
| Needlr_Reflection_ResolveDependent | 15.66 ns | 0.897 ns | 0.139 ns |  1.01 |    1 |         - |          NA |
| Needlr_SourceGen_ResolveDependent  | 15.35 ns | 0.249 ns | 0.065 ns |  0.99 |    1 |         - |          NA |
