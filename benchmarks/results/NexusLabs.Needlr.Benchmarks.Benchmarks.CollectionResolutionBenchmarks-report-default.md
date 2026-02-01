
BenchmarkDotNet v0.14.0, Ubuntu 24.04.3 LTS (Noble Numbat)
AMD EPYC 7763, 1 CPU, 4 logical and 2 physical cores
.NET SDK 10.0.102
  [Host]   : .NET 10.0.2 (10.0.225.61305), X64 RyuJIT AVX2
  ShortRun : .NET 10.0.2 (10.0.225.61305), X64 RyuJIT AVX2

Job=ShortRun  IterationCount=5  LaunchCount=1  
WarmupCount=3  

 Method                       | Mean     | Error    | StdDev   | Ratio | Rank | Gen0   | Allocated | Alloc Ratio |
----------------------------- |---------:|---------:|---------:|------:|-----:|-------:|----------:|------------:|
 ResolveCollection_Reflection | 45.95 ns | 0.409 ns | 0.063 ns |  1.00 |    1 | 0.0029 |      48 B |        1.00 |
 ResolveCollection_SourceGen  | 51.89 ns | 1.473 ns | 0.383 ns |  1.13 |    1 | 0.0029 |      48 B |        1.00 |
