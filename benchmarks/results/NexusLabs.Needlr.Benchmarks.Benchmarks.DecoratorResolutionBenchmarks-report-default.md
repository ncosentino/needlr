
BenchmarkDotNet v0.14.0, Ubuntu 24.04.3 LTS (Noble Numbat)
AMD EPYC 7763, 1 CPU, 4 logical and 2 physical cores
.NET SDK 10.0.102
  [Host]   : .NET 10.0.2 (10.0.225.61305), X64 RyuJIT AVX2
  ShortRun : .NET 10.0.2 (10.0.225.61305), X64 RyuJIT AVX2

Job=ShortRun  IterationCount=5  LaunchCount=1  
WarmupCount=3  

 Method     | Mean     | Error    | StdDev   | Ratio | Rank | Allocated | Alloc Ratio |
----------- |---------:|---------:|---------:|------:|-----:|----------:|------------:|
 Reflection | 14.45 ns | 0.319 ns | 0.083 ns |  1.00 |    1 |         - |          NA |
 SourceGen  | 15.10 ns | 0.353 ns | 0.092 ns |  1.05 |    1 |         - |          NA |
