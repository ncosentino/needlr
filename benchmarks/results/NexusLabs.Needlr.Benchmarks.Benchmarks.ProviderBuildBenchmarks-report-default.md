
BenchmarkDotNet v0.14.0, Ubuntu 24.04.5 LTS (Noble Numbat)
AMD EPYC 9V74, 1 CPU, 4 logical and 2 physical cores
.NET SDK 10.0.404
  [Host]   : .NET 10.0.15 (10.0.1526.53008), X64 RyuJIT AVX-512F+CD+BW+DQ+VL+VBMI
  ShortRun : .NET 10.0.15 (10.0.1526.53008), X64 RyuJIT AVX-512F+CD+BW+DQ+VL+VBMI

Job=ShortRun  IterationCount=5  LaunchCount=1  
WarmupCount=3  

 Method                              | Mean         | Error        | StdDev      | Ratio  | RatioSD | Rank | Gen0    | Gen1   | Allocated | Alloc Ratio |
------------------------------------ |-------------:|-------------:|------------:|-------:|--------:|-----:|--------:|-------:|----------:|------------:|
 ManualDI_ToServiceProvider          |     873.9 ns |     30.00 ns |     7.79 ns |   1.00 |    0.01 |    1 |  0.2689 | 0.0887 |   4.41 KB |        1.00 |
 Needlr_Reflection_ToServiceProvider | 570,837.2 ns | 10,996.00 ns | 1,701.64 ns | 653.26 |    5.67 |    3 | 13.6719 |      - | 252.82 KB |       57.38 |
 Needlr_SourceGen_ToServiceProvider  |  75,067.9 ns |  1,689.49 ns |   438.75 ns |  85.91 |    0.84 |    2 | 11.2305 | 1.4648 | 184.08 KB |       41.78 |
 Needlr_SourceGen_ToProvider         |  77,599.2 ns |  1,000.37 ns |   259.79 ns |  88.80 |    0.78 |    2 | 11.7188 | 1.4648 | 192.04 KB |       43.58 |
