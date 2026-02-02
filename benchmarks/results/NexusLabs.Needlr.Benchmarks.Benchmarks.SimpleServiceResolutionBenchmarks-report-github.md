```

BenchmarkDotNet v0.14.0, Ubuntu 24.04.3 LTS (Noble Numbat)
AMD EPYC 7763, 1 CPU, 4 logical and 2 physical cores
.NET SDK 10.0.102
  [Host]   : .NET 10.0.2 (10.0.225.61305), X64 RyuJIT AVX2
  ShortRun : .NET 10.0.2 (10.0.225.61305), X64 RyuJIT AVX2

Job=ShortRun  IterationCount=5  LaunchCount=1  
WarmupCount=3  

```
| Method                          | Mean     | Error    | StdDev   | Ratio | Rank | Allocated | Alloc Ratio |
|-------------------------------- |---------:|---------:|---------:|------:|-----:|----------:|------------:|
| ManualDI_ResolveSimple          | 16.49 ns | 0.692 ns | 0.180 ns |  1.00 |    1 |         - |          NA |
| Needlr_Reflection_ResolveSimple | 15.45 ns | 0.126 ns | 0.033 ns |  0.94 |    1 |         - |          NA |
| Needlr_SourceGen_ResolveSimple  | 15.00 ns | 0.057 ns | 0.009 ns |  0.91 |    1 |         - |          NA |
