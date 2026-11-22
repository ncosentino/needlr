```

BenchmarkDotNet v0.14.0, Ubuntu 24.04.5 LTS (Noble Numbat)
AMD EPYC 9V74, 1 CPU, 4 logical and 2 physical cores
.NET SDK 10.0.404
  [Host]   : .NET 10.0.15 (10.0.1526.53008), X64 RyuJIT AVX-512F+CD+BW+DQ+VL+VBMI
  ShortRun : .NET 10.0.15 (10.0.1526.53008), X64 RyuJIT AVX-512F+CD+BW+DQ+VL+VBMI

Job=ShortRun  IterationCount=5  LaunchCount=1  
WarmupCount=3  

```
| Method                          | Mean     | Error    | StdDev   | Ratio | Rank | Allocated | Alloc Ratio |
|-------------------------------- |---------:|---------:|---------:|------:|-----:|----------:|------------:|
| ManualDI_ResolveSimple          | 12.20 ns | 0.313 ns | 0.081 ns |  1.00 |    1 |         - |          NA |
| Needlr_Reflection_ResolveSimple | 14.17 ns | 0.105 ns | 0.027 ns |  1.16 |    1 |         - |          NA |
| Needlr_SourceGen_ResolveSimple  | 14.55 ns | 0.168 ns | 0.044 ns |  1.19 |    1 |         - |          NA |
