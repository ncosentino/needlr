```

BenchmarkDotNet v0.14.0, Ubuntu 24.04.4 LTS (Noble Numbat)
AMD EPYC 7763, 1 CPU, 4 logical and 2 physical cores
.NET SDK 10.0.203
  [Host]   : .NET 10.0.7 (10.0.726.21808), X64 RyuJIT AVX2
  ShortRun : .NET 10.0.7 (10.0.726.21808), X64 RyuJIT AVX2

Job=ShortRun  IterationCount=5  LaunchCount=1  
WarmupCount=3  

```
| Method                          | Mean     | Error    | StdDev   | Ratio | Rank | Allocated | Alloc Ratio |
|-------------------------------- |---------:|---------:|---------:|------:|-----:|----------:|------------:|
| ManualDI_ResolveSimple          | 15.25 ns | 0.421 ns | 0.109 ns |  1.00 |    1 |         - |          NA |
| Needlr_Reflection_ResolveSimple | 15.29 ns | 0.063 ns | 0.010 ns |  1.00 |    1 |         - |          NA |
| Needlr_SourceGen_ResolveSimple  | 15.17 ns | 0.500 ns | 0.130 ns |  0.99 |    1 |         - |          NA |
