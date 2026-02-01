
BenchmarkDotNet v0.14.0, Ubuntu 24.04.3 LTS (Noble Numbat)
AMD EPYC 7763, 1 CPU, 4 logical and 2 physical cores
.NET SDK 10.0.102
  [Host]   : .NET 10.0.2 (10.0.225.61305), X64 RyuJIT AVX2
  ShortRun : .NET 10.0.2 (10.0.225.61305), X64 RyuJIT AVX2

Job=ShortRun  IterationCount=5  LaunchCount=1  
WarmupCount=3  

 Method                      | Mean     | Error    | StdDev   | Ratio | RatioSD | Rank | Allocated | Alloc Ratio |
---------------------------- |---------:|---------:|---------:|------:|--------:|-----:|----------:|------------:|
 ResolveDecorated_Reflection | 15.58 ns | 1.322 ns | 0.343 ns |  1.00 |    0.03 |    1 |         - |          NA |
 ResolveDecorated_SourceGen  | 15.20 ns | 0.019 ns | 0.005 ns |  0.98 |    0.02 |    1 |         - |          NA |
