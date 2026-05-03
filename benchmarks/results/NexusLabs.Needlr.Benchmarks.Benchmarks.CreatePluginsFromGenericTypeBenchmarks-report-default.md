
BenchmarkDotNet v0.14.0, Ubuntu 24.04.4 LTS (Noble Numbat)
AMD EPYC 7763, 1 CPU, 4 logical and 2 physical cores
.NET SDK 10.0.203
  [Host]   : .NET 10.0.7 (10.0.726.21808), X64 RyuJIT AVX2
  ShortRun : .NET 10.0.7 (10.0.726.21808), X64 RyuJIT AVX2

Job=ShortRun  IterationCount=5  LaunchCount=1  
WarmupCount=3  

 Method                          | Mean        | Error     | StdDev   | Ratio | Rank | Gen0   | Allocated | Alloc Ratio |
-------------------------------- |------------:|----------:|---------:|------:|-----:|-------:|----------:|------------:|
 Reflection_AssemblyListProvided | 11,725.1 ns | 181.81 ns | 47.21 ns |  1.00 |    2 | 0.1678 |    3005 B |        1.00 |
 SourceGen_AssemblyListProvided  | 25,983.0 ns | 341.25 ns | 52.81 ns |  2.22 |    3 | 1.5564 |   26528 B |        8.83 |
 SourceGen_EmptyAssemblyList     | 24,154.1 ns | 345.02 ns | 53.39 ns |  2.06 |    3 | 1.5259 |   25528 B |        8.50 |
 SourceGen_ParameterlessOverload |    458.4 ns |   4.02 ns |  0.62 ns |  0.04 |    1 | 0.0257 |     440 B |        0.15 |
