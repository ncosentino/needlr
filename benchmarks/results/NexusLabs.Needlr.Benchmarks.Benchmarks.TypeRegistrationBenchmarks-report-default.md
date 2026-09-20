
BenchmarkDotNet v0.14.0, Ubuntu 24.04.5 LTS (Noble Numbat)
AMD EPYC 9V74, 1 CPU, 4 logical and 2 physical cores
.NET SDK 10.0.303
  [Host]   : .NET 10.0.11 (10.0.1126.37416), X64 RyuJIT AVX-512F+CD+BW+DQ+VL+VBMI
  ShortRun : .NET 10.0.11 (10.0.1126.37416), X64 RyuJIT AVX-512F+CD+BW+DQ+VL+VBMI

Job=ShortRun  IterationCount=5  LaunchCount=1  
WarmupCount=3  

 Method                          | Mean          | Error         | StdDev       | Ratio     | RatioSD | Rank | Gen0    | Gen1   | Allocated | Alloc Ratio |
-------------------------------- |--------------:|--------------:|-------------:|----------:|--------:|-----:|--------:|-------:|----------:|------------:|
 ManualDI_RegisterTypes          |      76.67 ns |      3.958 ns |     1.028 ns |      1.00 |    0.02 |    1 |  0.0315 |      - |     528 B |        1.00 |
 Needlr_Reflection_RegisterTypes | 905,501.22 ns | 46,907.559 ns | 7,258.997 ns | 11,812.70 |  167.84 |    3 | 35.1563 |      - |  589442 B |    1,116.37 |
 Needlr_SourceGen_RegisterTypes  |  11,053.42 ns |    628.377 ns |    97.242 ns |    144.20 |    2.10 |    2 |  1.8463 | 0.1526 |   30960 B |       58.64 |
