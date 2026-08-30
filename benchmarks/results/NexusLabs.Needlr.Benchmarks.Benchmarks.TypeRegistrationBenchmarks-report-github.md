```

BenchmarkDotNet v0.14.0, Ubuntu 24.04.4 LTS (Noble Numbat)
AMD EPYC 7763, 1 CPU, 4 logical and 2 physical cores
.NET SDK 10.0.302
  [Host]   : .NET 10.0.11 (10.0.1126.37416), X64 RyuJIT AVX2
  ShortRun : .NET 10.0.11 (10.0.1126.37416), X64 RyuJIT AVX2

Job=ShortRun  IterationCount=5  LaunchCount=1  
WarmupCount=3  

```
| Method                          | Mean           | Error        | StdDev      | Ratio     | RatioSD | Rank | Gen0    | Gen1   | Allocated | Alloc Ratio |
|-------------------------------- |---------------:|-------------:|------------:|----------:|--------:|-----:|--------:|-------:|----------:|------------:|
| ManualDI_RegisterTypes          |       112.4 ns |      2.05 ns |     0.53 ns |      1.00 |    0.01 |    1 |  0.0315 |      - |     528 B |        1.00 |
| Needlr_Reflection_RegisterTypes | 1,227,469.1 ns | 18,603.13 ns | 4,831.17 ns | 10,916.97 |   61.45 |    3 | 35.1563 |      - |  589442 B |    1,116.37 |
| Needlr_SourceGen_RegisterTypes  |    17,858.2 ns |    272.77 ns |    70.84 ns |    158.83 |    0.90 |    2 |  1.8311 | 0.1221 |   30960 B |       58.64 |
