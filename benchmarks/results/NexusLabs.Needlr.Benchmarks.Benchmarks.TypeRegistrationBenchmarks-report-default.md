
BenchmarkDotNet v0.14.0, Ubuntu 24.04.4 LTS (Noble Numbat)
AMD EPYC 9V74, 1 CPU, 4 logical and 2 physical cores
.NET SDK 10.0.202
  [Host]   : .NET 10.0.6 (10.0.626.17701), X64 RyuJIT AVX2
  ShortRun : .NET 10.0.6 (10.0.626.17701), X64 RyuJIT AVX2

Job=ShortRun  IterationCount=5  LaunchCount=1  
WarmupCount=3  

 Method                          | Mean           | Error       | StdDev      | Ratio     | RatioSD | Rank | Gen0    | Gen1   | Allocated | Alloc Ratio |
-------------------------------- |---------------:|------------:|------------:|----------:|--------:|-----:|--------:|-------:|----------:|------------:|
 ManualDI_RegisterTypes          |       102.9 ns |    14.15 ns |     3.67 ns |      1.00 |    0.05 |    1 |  0.0315 |      - |     528 B |        1.00 |
 Needlr_Reflection_RegisterTypes | 1,127,603.2 ns | 7,415.16 ns | 1,147.50 ns | 10,971.45 |  364.23 |    3 | 35.1563 |      - |  589442 B |    1,116.37 |
 Needlr_SourceGen_RegisterTypes  |    14,878.5 ns |   223.07 ns |    57.93 ns |    144.77 |    4.81 |    2 |  1.8158 | 0.1373 |   30440 B |       57.65 |
