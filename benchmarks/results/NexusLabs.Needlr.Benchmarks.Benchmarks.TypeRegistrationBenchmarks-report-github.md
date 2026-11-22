```

BenchmarkDotNet v0.14.0, Ubuntu 24.04.5 LTS (Noble Numbat)
AMD EPYC 9V74, 1 CPU, 4 logical and 2 physical cores
.NET SDK 10.0.404
  [Host]   : .NET 10.0.15 (10.0.1526.53008), X64 RyuJIT AVX-512F+CD+BW+DQ+VL+VBMI
  ShortRun : .NET 10.0.15 (10.0.1526.53008), X64 RyuJIT AVX-512F+CD+BW+DQ+VL+VBMI

Job=ShortRun  IterationCount=5  LaunchCount=1  
WarmupCount=3  

```
| Method                          | Mean          | Error          | StdDev        | Ratio     | RatioSD | Rank | Gen0    | Gen1   | Allocated | Alloc Ratio |
|-------------------------------- |--------------:|---------------:|--------------:|----------:|--------:|-----:|--------:|-------:|----------:|------------:|
| ManualDI_RegisterTypes          |      77.62 ns |       6.787 ns |      1.763 ns |      1.00 |    0.03 |    1 |  0.0315 |      - |     528 B |        1.00 |
| Needlr_Reflection_RegisterTypes | 924,992.06 ns | 109,455.338 ns | 16,938.335 ns | 11,921.64 |  314.91 |    3 | 35.1563 |      - |  589442 B |    1,116.37 |
| Needlr_SourceGen_RegisterTypes  |  11,553.97 ns |     174.331 ns |     26.978 ns |    148.91 |    3.11 |    2 |  1.8463 | 0.1526 |   30960 B |       58.64 |
