```

BenchmarkDotNet v0.14.0, Ubuntu 24.04.4 LTS (Noble Numbat)
AMD EPYC 7763, 1 CPU, 4 logical and 2 physical cores
.NET SDK 10.0.301
  [Host]   : .NET 10.0.9 (10.0.926.27113), X64 RyuJIT AVX2
  ShortRun : .NET 10.0.9 (10.0.926.27113), X64 RyuJIT AVX2

Job=ShortRun  IterationCount=5  LaunchCount=1  
WarmupCount=3  

```
| Method                          | Mean            | Error            | StdDev         | Ratio     | RatioSD  | Rank | Gen0    | Gen1   | Allocated | Alloc Ratio |
|-------------------------------- |----------------:|-----------------:|---------------:|----------:|---------:|-----:|--------:|-------:|----------:|------------:|
| ManualDI_RegisterTypes          |        96.75 ns |         3.836 ns |       0.996 ns |      1.00 |     0.01 |    1 |  0.0315 |      - |     528 B |        1.00 |
| Needlr_Reflection_RegisterTypes | 1,567,183.21 ns | 1,502,687.821 ns | 390,243.256 ns | 16,199.02 | 3,685.57 |    3 | 31.2500 |      - |  589831 B |    1,117.10 |
| Needlr_SourceGen_RegisterTypes  |    17,030.49 ns |       238.853 ns |      36.963 ns |    176.03 |     1.70 |    2 |  1.8005 | 0.1221 |   30440 B |       57.65 |
