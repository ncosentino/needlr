
BenchmarkDotNet v0.14.0, Ubuntu 24.04.3 LTS (Noble Numbat)
AMD EPYC 7763, 1 CPU, 4 logical and 2 physical cores
.NET SDK 10.0.102
  [Host]   : .NET 10.0.2 (10.0.225.61305), X64 RyuJIT AVX2
  ShortRun : .NET 10.0.2 (10.0.225.61305), X64 RyuJIT AVX2

Job=ShortRun  IterationCount=5  LaunchCount=1  
WarmupCount=3  

 Method                        | Mean     | Error    | StdDev   | Ratio | Rank | Gen0   | Allocated | Alloc Ratio |
------------------------------ |---------:|---------:|---------:|------:|-----:|-------:|----------:|------------:|
 ResolveOpenGeneric_Reflection | 19.86 ns | 0.406 ns | 0.105 ns |  1.00 |    1 | 0.0014 |      24 B |        1.00 |
 ResolveOpenGeneric_SourceGen  | 18.88 ns | 0.102 ns | 0.016 ns |  0.95 |    1 | 0.0014 |      24 B |        1.00 |
