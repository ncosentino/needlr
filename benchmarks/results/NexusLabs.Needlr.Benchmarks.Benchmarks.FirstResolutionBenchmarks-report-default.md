
BenchmarkDotNet v0.14.0, Ubuntu 24.04.3 LTS (Noble Numbat)
AMD EPYC 7763, 1 CPU, 4 logical and 2 physical cores
.NET SDK 10.0.102
  [Host]   : .NET 10.0.2 (10.0.225.61305), X64 RyuJIT AVX2
  ShortRun : .NET 10.0.2 (10.0.225.61305), X64 RyuJIT AVX2

Job=ShortRun  InvocationCount=1  IterationCount=5  
LaunchCount=1  UnrollFactor=1  WarmupCount=3  

 Method                          | Mean       | Error    | StdDev   | Ratio | Rank | Allocated | Alloc Ratio |
-------------------------------- |-----------:|---------:|---------:|------:|-----:|----------:|------------:|
 BuildAndResolveFirst_Reflection | 4,347.9 μs | 91.50 μs | 23.76 μs |  1.00 |    2 | 977.76 KB |        1.00 |
 BuildAndResolveFirst_SourceGen  |   397.6 μs | 38.47 μs |  5.95 μs |  0.09 |    1 | 188.23 KB |        0.19 |
