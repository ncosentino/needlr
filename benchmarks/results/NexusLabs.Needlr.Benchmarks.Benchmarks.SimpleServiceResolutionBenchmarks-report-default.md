
BenchmarkDotNet v0.14.0, Ubuntu 24.04.3 LTS (Noble Numbat)
AMD EPYC 7763, 1 CPU, 4 logical and 2 physical cores
.NET SDK 10.0.102
  [Host]   : .NET 10.0.2 (10.0.225.61305), X64 RyuJIT AVX2
  ShortRun : .NET 10.0.2 (10.0.225.61305), X64 RyuJIT AVX2

Job=ShortRun  IterationCount=5  LaunchCount=1  
WarmupCount=3  

 Method                   | Mean     | Error    | StdDev   | Ratio | Rank | Allocated | Alloc Ratio |
------------------------- |---------:|---------:|---------:|------:|-----:|----------:|------------:|
 ResolveSimple_Reflection | 14.71 ns | 0.652 ns | 0.169 ns |  1.00 |    1 |         - |          NA |
 ResolveSimple_SourceGen  | 14.97 ns | 0.044 ns | 0.012 ns |  1.02 |    1 |         - |          NA |
