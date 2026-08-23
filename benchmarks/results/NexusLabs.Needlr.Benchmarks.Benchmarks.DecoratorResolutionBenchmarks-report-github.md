```

BenchmarkDotNet v0.14.0, Ubuntu 24.04.4 LTS (Noble Numbat)
AMD EPYC 7763, 1 CPU, 4 logical and 2 physical cores
.NET SDK 10.0.302
  [Host]   : .NET 10.0.11 (10.0.1126.37416), X64 RyuJIT AVX2
  ShortRun : .NET 10.0.11 (10.0.1126.37416), X64 RyuJIT AVX2

Job=ShortRun  IterationCount=5  LaunchCount=1  
WarmupCount=3  

```
| Method                             | Mean     | Error    | StdDev   | Ratio | RatioSD | Rank | Allocated | Alloc Ratio |
|----------------------------------- |---------:|---------:|---------:|------:|--------:|-----:|----------:|------------:|
| ManualDI_ResolveDecorated          | 14.64 ns | 0.040 ns | 0.011 ns |  1.00 |    0.00 |    1 |         - |          NA |
| Needlr_Reflection_ResolveDecorated | 15.22 ns | 0.334 ns | 0.052 ns |  1.04 |    0.00 |    1 |         - |          NA |
| Needlr_SourceGen_ResolveDecorated  | 15.40 ns | 1.034 ns | 0.268 ns |  1.05 |    0.02 |    1 |         - |          NA |
