
BenchmarkDotNet v0.14.0, Ubuntu 24.04.4 LTS (Noble Numbat)
AMD EPYC 7763, 1 CPU, 4 logical and 2 physical cores
.NET SDK 10.0.302
  [Host]   : .NET 10.0.11 (10.0.1126.37416), X64 RyuJIT AVX2
  ShortRun : .NET 10.0.11 (10.0.1126.37416), X64 RyuJIT AVX2

Job=ShortRun  IterationCount=5  LaunchCount=1  
WarmupCount=3  

 Method                          | Mean        | Error     | StdDev    | Ratio | Rank | Gen0   | Allocated | Alloc Ratio |
-------------------------------- |------------:|----------:|----------:|------:|-----:|-------:|----------:|------------:|
 Reflection_AssemblyListProvided | 11,740.4 ns | 120.45 ns |  18.64 ns |  1.00 |    2 | 0.1678 |    3005 B |        1.00 |
 SourceGen_AssemblyListProvided  | 26,166.9 ns | 267.79 ns |  69.55 ns |  2.23 |    3 | 1.5564 |   26528 B |        8.83 |
 SourceGen_EmptyAssemblyList     | 25,684.7 ns | 387.00 ns | 100.50 ns |  2.19 |    3 | 1.5259 |   25528 B |        8.50 |
 SourceGen_ParameterlessOverload |    468.9 ns |  12.55 ns |   1.94 ns |  0.04 |    1 | 0.0262 |     440 B |        0.15 |
