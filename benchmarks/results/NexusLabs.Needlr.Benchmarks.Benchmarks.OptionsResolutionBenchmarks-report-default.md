
BenchmarkDotNet v0.14.0, Ubuntu 24.04.3 LTS (Noble Numbat)
AMD EPYC 7763, 1 CPU, 4 logical and 2 physical cores
.NET SDK 10.0.102
  [Host]   : .NET 10.0.2 (10.0.225.61305), X64 RyuJIT AVX2
  ShortRun : .NET 10.0.2 (10.0.225.61305), X64 RyuJIT AVX2

Job=ShortRun  IterationCount=5  LaunchCount=1  
WarmupCount=3  

 Method                    | Mean     | Error    | StdDev   | Ratio | RatioSD | Rank | Allocated | Alloc Ratio |
-------------------------- |---------:|---------:|---------:|------:|--------:|-----:|----------:|------------:|
 ResolveOptions_Reflection | 16.68 ns | 0.051 ns | 0.013 ns |  1.00 |    0.00 |    1 |         - |          NA |
 ResolveOptions_SourceGen  | 20.39 ns | 2.027 ns | 0.526 ns |  1.22 |    0.03 |    1 |         - |          NA |
