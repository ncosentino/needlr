
BenchmarkDotNet v0.14.0, Ubuntu 24.04.3 LTS (Noble Numbat)
AMD EPYC 7763, 1 CPU, 4 logical and 2 physical cores
.NET SDK 10.0.102
  [Host]   : .NET 10.0.2 (10.0.225.61305), X64 RyuJIT AVX2
  ShortRun : .NET 10.0.2 (10.0.225.61305), X64 RyuJIT AVX2

Job=ShortRun  IterationCount=5  LaunchCount=1  
WarmupCount=3  

 Method     | Mean     | Error    | StdDev   | Ratio | Rank | Allocated | Alloc Ratio |
----------- |---------:|---------:|---------:|------:|-----:|----------:|------------:|
 Reflection | 15.33 ns | 0.092 ns | 0.024 ns |  1.00 |    1 |         - |          NA |
 SourceGen  | 15.06 ns | 0.326 ns | 0.085 ns |  0.98 |    1 |         - |          NA |
