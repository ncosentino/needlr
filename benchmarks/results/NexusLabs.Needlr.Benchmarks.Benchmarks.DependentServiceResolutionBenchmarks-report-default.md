
BenchmarkDotNet v0.14.0, Ubuntu 24.04.3 LTS (Noble Numbat)
AMD EPYC 7763, 1 CPU, 4 logical and 2 physical cores
.NET SDK 10.0.102
  [Host]   : .NET 10.0.2 (10.0.225.61305), X64 RyuJIT AVX2
  ShortRun : .NET 10.0.2 (10.0.225.61305), X64 RyuJIT AVX2

Job=ShortRun  IterationCount=5  LaunchCount=1  
WarmupCount=3  

 Method                      | Mean     | Error    | StdDev   | Ratio | Rank | Allocated | Alloc Ratio |
---------------------------- |---------:|---------:|---------:|------:|-----:|----------:|------------:|
 ResolveDependent_Reflection | 14.33 ns | 0.396 ns | 0.103 ns |  1.00 |    1 |         - |          NA |
 ResolveDependent_SourceGen  | 14.48 ns | 0.360 ns | 0.094 ns |  1.01 |    1 |         - |          NA |
