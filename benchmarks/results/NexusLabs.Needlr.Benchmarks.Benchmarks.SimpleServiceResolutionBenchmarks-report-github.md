```

BenchmarkDotNet v0.14.0, Ubuntu 24.04.4 LTS (Noble Numbat)
AMD EPYC 9V74, 1 CPU, 4 logical and 2 physical cores
.NET SDK 10.0.201
  [Host]   : .NET 10.0.5 (10.0.526.15411), X64 RyuJIT AVX2
  ShortRun : .NET 10.0.5 (10.0.526.15411), X64 RyuJIT AVX2

Job=ShortRun  IterationCount=5  LaunchCount=1  
WarmupCount=3  

```
| Method                          | Mean     | Error    | StdDev   | Ratio | Rank | Allocated | Alloc Ratio |
|-------------------------------- |---------:|---------:|---------:|------:|-----:|----------:|------------:|
| ManualDI_ResolveSimple          | 19.25 ns | 0.697 ns | 0.181 ns |  1.00 |    1 |         - |          NA |
| Needlr_Reflection_ResolveSimple | 18.73 ns | 0.076 ns | 0.020 ns |  0.97 |    1 |         - |          NA |
| Needlr_SourceGen_ResolveSimple  | 19.40 ns | 0.588 ns | 0.153 ns |  1.01 |    1 |         - |          NA |
