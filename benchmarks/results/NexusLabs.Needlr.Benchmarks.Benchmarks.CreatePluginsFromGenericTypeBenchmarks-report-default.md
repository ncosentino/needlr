
BenchmarkDotNet v0.14.0, Ubuntu 24.04.3 LTS (Noble Numbat)
AMD EPYC 7763, 1 CPU, 4 logical and 2 physical cores
.NET SDK 10.0.102
  [Host]   : .NET 10.0.2 (10.0.225.61305), X64 RyuJIT AVX2
  ShortRun : .NET 10.0.2 (10.0.225.61305), X64 RyuJIT AVX2

Job=ShortRun  IterationCount=5  LaunchCount=1  
WarmupCount=3  

 Method                          | Mean        | Error     | StdDev    | Ratio | Rank | Gen0   | Allocated | Alloc Ratio |
-------------------------------- |------------:|----------:|----------:|------:|-----:|-------:|----------:|------------:|
 Reflection_AssemblyListProvided | 31,149.9 ns | 392.69 ns | 101.98 ns |  1.00 |    3 | 0.3662 |    6552 B |        1.00 |
 SourceGen_AssemblyListProvided  | 23,907.3 ns |  83.29 ns |  21.63 ns |  0.77 |    2 | 1.4038 |   23528 B |        3.59 |
 SourceGen_EmptyAssemblyList     | 22,349.9 ns | 219.10 ns |  33.91 ns |  0.72 |    2 | 1.3428 |   22528 B |        3.44 |
 SourceGen_ParameterlessOverload |    448.7 ns |   3.51 ns |   0.54 ns |  0.01 |    1 | 0.0257 |     440 B |        0.07 |
