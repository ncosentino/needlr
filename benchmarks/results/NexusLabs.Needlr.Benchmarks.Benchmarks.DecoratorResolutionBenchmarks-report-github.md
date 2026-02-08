```

BenchmarkDotNet v0.14.0, Ubuntu 24.04.3 LTS (Noble Numbat)
AMD EPYC 7763, 1 CPU, 4 logical and 2 physical cores
.NET SDK 10.0.102
  [Host]   : .NET 10.0.2 (10.0.225.61305), X64 RyuJIT AVX2
  ShortRun : .NET 10.0.2 (10.0.225.61305), X64 RyuJIT AVX2

Job=ShortRun  IterationCount=5  LaunchCount=1  
WarmupCount=3  

```
| Method                             | Mean     | Error    | StdDev   | Ratio | Rank | Allocated | Alloc Ratio |
|----------------------------------- |---------:|---------:|---------:|------:|-----:|----------:|------------:|
| ManualDI_ResolveDecorated          | 14.45 ns | 0.545 ns | 0.142 ns |  1.00 |    1 |         - |          NA |
| Needlr_Reflection_ResolveDecorated | 15.34 ns | 0.195 ns | 0.030 ns |  1.06 |    1 |         - |          NA |
| Needlr_SourceGen_ResolveDecorated  | 15.13 ns | 0.205 ns | 0.053 ns |  1.05 |    1 |         - |          NA |
