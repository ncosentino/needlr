
BenchmarkDotNet v0.14.0, Ubuntu 24.04.3 LTS (Noble Numbat)
AMD EPYC 7763, 1 CPU, 4 logical and 2 physical cores
.NET SDK 10.0.102
  [Host]   : .NET 10.0.2 (10.0.225.61305), X64 RyuJIT AVX2
  ShortRun : .NET 10.0.2 (10.0.225.61305), X64 RyuJIT AVX2

Job=ShortRun  IterationCount=5  LaunchCount=1  
WarmupCount=3  

 Method                   | Mean        | Error     | StdDev   | Ratio | Rank | Gen0    | Gen1   | Allocated | Alloc Ratio |
------------------------- |------------:|----------:|---------:|------:|-----:|--------:|-------:|----------:|------------:|
 RegisterTypes_Reflection | 1,188.42 μs | 27.391 μs | 4.239 μs |  1.00 |    2 | 35.1563 | 3.9063 |    575 KB |        1.00 |
 RegisterTypes_SourceGen  |    17.77 μs |  0.514 μs | 0.133 μs |  0.01 |    1 |  1.8005 | 0.1221 |  29.73 KB |        0.05 |
