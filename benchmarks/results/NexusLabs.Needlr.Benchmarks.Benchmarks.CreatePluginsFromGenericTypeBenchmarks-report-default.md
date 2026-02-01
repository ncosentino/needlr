
BenchmarkDotNet v0.14.0, Ubuntu 24.04.3 LTS (Noble Numbat)
AMD EPYC 7763, 1 CPU, 4 logical and 2 physical cores
.NET SDK 10.0.102
  [Host]   : .NET 10.0.2 (10.0.225.61305), X64 RyuJIT AVX2
  ShortRun : .NET 10.0.2 (10.0.225.61305), X64 RyuJIT AVX2

Job=ShortRun  IterationCount=5  LaunchCount=1  
WarmupCount=3  

 Method                          | Mean        | Error     | StdDev   | Ratio | Rank | Gen0   | Allocated | Alloc Ratio |
-------------------------------- |------------:|----------:|---------:|------:|-----:|-------:|----------:|------------:|
 Reflection                      | 20,328.2 ns |  61.78 ns |  9.56 ns |  1.00 |    2 | 0.2747 |    4719 B |        1.00 |
 SourceGen_AssemblyListProvided  | 19,341.6 ns | 309.25 ns | 47.86 ns |  0.95 |    2 | 1.1292 |   19328 B |        4.10 |
 SourceGen_EmptyAssemblyList     | 17,983.2 ns | 485.26 ns | 75.09 ns |  0.88 |    2 | 1.0681 |   18328 B |        3.88 |
 SourceGen_ParameterlessOverload |    403.2 ns |   8.04 ns |  1.24 ns |  0.02 |    1 | 0.0262 |     440 B |        0.09 |
