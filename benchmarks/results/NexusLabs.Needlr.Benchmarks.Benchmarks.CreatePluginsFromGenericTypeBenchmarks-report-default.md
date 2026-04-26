
BenchmarkDotNet v0.14.0, Ubuntu 24.04.4 LTS (Noble Numbat)
AMD EPYC 7763, 1 CPU, 4 logical and 2 physical cores
.NET SDK 10.0.203
  [Host]   : .NET 10.0.7 (10.0.726.21808), X64 RyuJIT AVX2
  ShortRun : .NET 10.0.7 (10.0.726.21808), X64 RyuJIT AVX2

Job=ShortRun  IterationCount=5  LaunchCount=1  
WarmupCount=3  

 Method                          | Mean        | Error     | StdDev   | Ratio | Rank | Gen0   | Allocated | Alloc Ratio |
-------------------------------- |------------:|----------:|---------:|------:|-----:|-------:|----------:|------------:|
 Reflection_AssemblyListProvided | 11,815.0 ns |  96.06 ns | 14.86 ns |  1.00 |    2 | 0.1678 |    3005 B |        1.00 |
 SourceGen_AssemblyListProvided  | 26,577.9 ns | 154.50 ns | 40.12 ns |  2.25 |    3 | 1.5564 |   26528 B |        8.83 |
 SourceGen_EmptyAssemblyList     | 24,755.5 ns | 248.43 ns | 64.52 ns |  2.10 |    3 | 1.5259 |   25528 B |        8.50 |
 SourceGen_ParameterlessOverload |    484.7 ns |   3.59 ns |  0.93 ns |  0.04 |    1 | 0.0257 |     440 B |        0.15 |
