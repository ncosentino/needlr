```

BenchmarkDotNet v0.14.0, Ubuntu 24.04.4 LTS (Noble Numbat)
AMD EPYC 9V74, 1 CPU, 4 logical and 2 physical cores
.NET SDK 10.0.302
  [Host]   : .NET 10.0.10 (10.0.1026.32716), X64 RyuJIT AVX2
  ShortRun : .NET 10.0.10 (10.0.1026.32716), X64 RyuJIT AVX2

Job=ShortRun  IterationCount=5  LaunchCount=1  
WarmupCount=3  

```
| Method                          | Mean     | Error    | StdDev   | Ratio | Rank | Allocated | Alloc Ratio |
|-------------------------------- |---------:|---------:|---------:|------:|-----:|----------:|------------:|
| ManualDI_ResolveSimple          | 15.46 ns | 0.035 ns | 0.009 ns |  1.00 |    1 |         - |          NA |
| Needlr_Reflection_ResolveSimple | 18.98 ns | 0.070 ns | 0.011 ns |  1.23 |    1 |         - |          NA |
| Needlr_SourceGen_ResolveSimple  | 16.44 ns | 0.089 ns | 0.023 ns |  1.06 |    1 |         - |          NA |
