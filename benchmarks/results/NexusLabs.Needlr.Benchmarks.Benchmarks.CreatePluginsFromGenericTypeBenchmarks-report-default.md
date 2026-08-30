
BenchmarkDotNet v0.14.0, Ubuntu 24.04.4 LTS (Noble Numbat)
AMD EPYC 7763, 1 CPU, 4 logical and 2 physical cores
.NET SDK 10.0.302
  [Host]   : .NET 10.0.11 (10.0.1126.37416), X64 RyuJIT AVX2
  ShortRun : .NET 10.0.11 (10.0.1126.37416), X64 RyuJIT AVX2

Job=ShortRun  IterationCount=5  LaunchCount=1  
WarmupCount=3  

 Method                          | Mean        | Error     | StdDev    | Ratio | Rank | Gen0   | Allocated | Alloc Ratio |
-------------------------------- |------------:|----------:|----------:|------:|-----:|-------:|----------:|------------:|
 Reflection_AssemblyListProvided | 11,787.1 ns | 272.97 ns |  42.24 ns |  1.00 |    2 | 0.1678 |    3005 B |        1.00 |
 SourceGen_AssemblyListProvided  | 26,935.2 ns |  73.56 ns |  11.38 ns |  2.29 |    3 | 1.5564 |   26528 B |        8.83 |
 SourceGen_EmptyAssemblyList     | 26,685.2 ns | 446.81 ns | 116.03 ns |  2.26 |    3 | 1.5259 |   25528 B |        8.50 |
 SourceGen_ParameterlessOverload |    482.2 ns |   3.75 ns |   0.97 ns |  0.04 |    1 | 0.0257 |     440 B |        0.15 |
