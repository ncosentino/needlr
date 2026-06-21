```

BenchmarkDotNet v0.14.0, Ubuntu 24.04.4 LTS (Noble Numbat)
AMD EPYC 7763, 1 CPU, 4 logical and 2 physical cores
.NET SDK 10.0.301
  [Host]   : .NET 10.0.9 (10.0.926.27113), X64 RyuJIT AVX2
  ShortRun : .NET 10.0.9 (10.0.926.27113), X64 RyuJIT AVX2

Job=ShortRun  IterationCount=5  LaunchCount=1  
WarmupCount=3  

```
| Method                          | Mean           | Error           | StdDev        | Ratio     | RatioSD  | Rank | Gen0    | Gen1   | Allocated | Alloc Ratio |
|-------------------------------- |---------------:|----------------:|--------------:|----------:|---------:|-----:|--------:|-------:|----------:|------------:|
| ManualDI_RegisterTypes          |       100.1 ns |         6.68 ns |       1.74 ns |      1.00 |     0.02 |    1 |  0.0315 |      - |     528 B |        1.00 |
| Needlr_Reflection_RegisterTypes | 1,732,280.1 ns | 1,777,901.19 ns | 461,715.29 ns | 17,312.87 | 4,221.76 |    3 | 31.2500 |      - |  589831 B |    1,117.10 |
| Needlr_SourceGen_RegisterTypes  |    17,476.9 ns |       382.23 ns |      99.26 ns |    174.67 |     2.90 |    2 |  1.8005 | 0.1221 |   30440 B |       57.65 |
