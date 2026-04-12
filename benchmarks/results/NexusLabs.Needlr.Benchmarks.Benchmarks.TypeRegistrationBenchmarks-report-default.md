
BenchmarkDotNet v0.14.0, Ubuntu 24.04.4 LTS (Noble Numbat)
AMD EPYC 9V74, 1 CPU, 4 logical and 2 physical cores
.NET SDK 10.0.201
  [Host]   : .NET 10.0.5 (10.0.526.15411), X64 RyuJIT AVX2
  ShortRun : .NET 10.0.5 (10.0.526.15411), X64 RyuJIT AVX2

Job=ShortRun  IterationCount=5  LaunchCount=1  
WarmupCount=3  

 Method                          | Mean            | Error      | StdDev     | Ratio     | RatioSD | Rank | Gen0    | Gen1   | Allocated | Alloc Ratio |
-------------------------------- |----------------:|-----------:|-----------:|----------:|--------:|-----:|--------:|-------:|----------:|------------:|
 ManualDI_RegisterTypes          |        95.61 ns |   0.592 ns |   0.154 ns |      1.00 |    0.00 |    1 |  0.0315 |      - |     528 B |        1.00 |
 Needlr_Reflection_RegisterTypes | 1,119,978.85 ns | 887.873 ns | 137.399 ns | 11,714.05 |   17.33 |    3 | 35.1563 | 3.9063 |  589218 B |    1,115.94 |
 Needlr_SourceGen_RegisterTypes  |    14,776.28 ns | 277.600 ns |  72.092 ns |    154.55 |    0.72 |    2 |  1.8158 | 0.1373 |   30440 B |       57.65 |
