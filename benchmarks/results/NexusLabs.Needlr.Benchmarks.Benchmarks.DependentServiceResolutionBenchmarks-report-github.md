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
| ManualDI_ResolveDependent          | 15.95 ns | 0.238 ns | 0.037 ns |  1.00 |    1 |         - |          NA |
| Needlr_Reflection_ResolveDependent | 14.87 ns | 0.738 ns | 0.192 ns |  0.93 |    1 |         - |          NA |
| Needlr_SourceGen_ResolveDependent  | 14.81 ns | 0.411 ns | 0.107 ns |  0.93 |    1 |         - |          NA |
