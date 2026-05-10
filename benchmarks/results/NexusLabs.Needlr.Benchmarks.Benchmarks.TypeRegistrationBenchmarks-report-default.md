
BenchmarkDotNet v0.14.0, Ubuntu 24.04.4 LTS (Noble Numbat)
AMD EPYC 9V74, 1 CPU, 4 logical and 2 physical cores
.NET SDK 10.0.203
  [Host]   : .NET 10.0.7 (10.0.726.21808), X64 RyuJIT AVX2
  ShortRun : .NET 10.0.7 (10.0.726.21808), X64 RyuJIT AVX2

Job=ShortRun  IterationCount=5  LaunchCount=1  
WarmupCount=3  

 Method                          | Mean           | Error           | StdDev        | Ratio     | RatioSD  | Rank | Gen0    | Gen1   | Allocated | Alloc Ratio |
-------------------------------- |---------------:|----------------:|--------------:|----------:|---------:|-----:|--------:|-------:|----------:|------------:|
 ManualDI_RegisterTypes          |       104.6 ns |         6.88 ns |       1.79 ns |      1.00 |     0.02 |    1 |  0.0315 |      - |     528 B |        1.00 |
 Needlr_Reflection_RegisterTypes | 1,779,705.5 ns | 2,271,332.29 ns | 589,857.78 ns | 17,011.63 | 5,154.69 |    3 | 31.2500 |      - |  589831 B |    1,117.10 |
 Needlr_SourceGen_RegisterTypes  |    14,499.1 ns |       220.60 ns |      57.29 ns |    138.59 |     2.25 |    2 |  1.8158 | 0.1373 |   30440 B |       57.65 |
