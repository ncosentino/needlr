
BenchmarkDotNet v0.14.0, Ubuntu 24.04.4 LTS (Noble Numbat)
AMD EPYC 7763, 1 CPU, 4 logical and 2 physical cores
.NET SDK 10.0.302
  [Host]   : .NET 10.0.11 (10.0.1126.37416), X64 RyuJIT AVX2
  ShortRun : .NET 10.0.11 (10.0.1126.37416), X64 RyuJIT AVX2

Job=ShortRun  IterationCount=5  LaunchCount=1  
WarmupCount=3  

 Method                          | Mean            | Error         | StdDev       | Ratio     | RatioSD | Rank | Gen0    | Gen1   | Allocated | Alloc Ratio |
-------------------------------- |----------------:|--------------:|-------------:|----------:|--------:|-----:|--------:|-------:|----------:|------------:|
 ManualDI_RegisterTypes          |        97.73 ns |      3.537 ns |     0.919 ns |      1.00 |    0.01 |    1 |  0.0315 |      - |     528 B |        1.00 |
 Needlr_Reflection_RegisterTypes | 1,209,315.27 ns | 34,140.337 ns | 8,866.137 ns | 12,374.78 |  134.27 |    3 | 35.1563 |      - |  589442 B |    1,116.37 |
 Needlr_SourceGen_RegisterTypes  |    17,211.21 ns |    933.735 ns |   242.488 ns |    176.12 |    2.72 |    2 |  1.8311 | 0.1221 |   30960 B |       58.64 |
