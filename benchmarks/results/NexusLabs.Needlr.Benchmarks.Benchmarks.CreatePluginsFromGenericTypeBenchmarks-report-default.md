
BenchmarkDotNet v0.14.0, Ubuntu 24.04.3 LTS (Noble Numbat)
AMD EPYC 7763, 1 CPU, 4 logical and 2 physical cores
.NET SDK 10.0.102
  [Host]   : .NET 10.0.2 (10.0.225.61305), X64 RyuJIT AVX2
  ShortRun : .NET 10.0.2 (10.0.225.61305), X64 RyuJIT AVX2

Job=ShortRun  IterationCount=5  LaunchCount=1  
WarmupCount=3  

 Method                          | Mean        | Error     | StdDev    | Ratio | Rank | Gen0   | Allocated | Alloc Ratio |
-------------------------------- |------------:|----------:|----------:|------:|-----:|-------:|----------:|------------:|
 Reflection_AssemblyListProvided | 29,531.5 ns | 625.21 ns |  96.75 ns |  1.00 |    2 | 0.3662 |    6437 B |        1.00 |
 SourceGen_AssemblyListProvided  | 23,724.4 ns | 495.43 ns | 128.66 ns |  0.80 |    2 | 1.4038 |   23528 B |        3.66 |
 SourceGen_EmptyAssemblyList     | 22,590.2 ns | 247.18 ns |  64.19 ns |  0.76 |    2 | 1.3428 |   22528 B |        3.50 |
 SourceGen_ParameterlessOverload |    435.1 ns |   6.44 ns |   1.00 ns |  0.01 |    1 | 0.0262 |     440 B |        0.07 |
