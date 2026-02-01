
BenchmarkDotNet v0.14.0, Ubuntu 24.04.3 LTS (Noble Numbat)
AMD EPYC 7763, 1 CPU, 4 logical and 2 physical cores
.NET SDK 10.0.102
  [Host]   : .NET 10.0.2 (10.0.225.61305), X64 RyuJIT AVX2
  ShortRun : .NET 10.0.2 (10.0.225.61305), X64 RyuJIT AVX2

Job=ShortRun  InvocationCount=1  IterationCount=5  
LaunchCount=1  UnrollFactor=1  WarmupCount=3  

 Method                          | Mean       | Error    | StdDev   | Ratio | Rank | Allocated  | Alloc Ratio |
-------------------------------- |-----------:|---------:|---------:|------:|-----:|-----------:|------------:|
 BuildAndResolveFirst_Reflection | 5,631.4 μs | 84.66 μs | 21.98 μs |  1.00 |    2 | 1226.41 KB |        1.00 |
 BuildAndResolveFirst_SourceGen  |   479.4 μs | 37.45 μs |  5.79 μs |  0.09 |    1 |     202 KB |        0.16 |
