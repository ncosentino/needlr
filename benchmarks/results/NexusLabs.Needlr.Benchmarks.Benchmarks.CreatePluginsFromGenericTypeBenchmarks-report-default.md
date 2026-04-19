
BenchmarkDotNet v0.14.0, Ubuntu 24.04.4 LTS (Noble Numbat)
AMD EPYC 9V74, 1 CPU, 4 logical and 2 physical cores
.NET SDK 10.0.202
  [Host]   : .NET 10.0.6 (10.0.626.17701), X64 RyuJIT AVX2
  ShortRun : .NET 10.0.6 (10.0.626.17701), X64 RyuJIT AVX2

Job=ShortRun  IterationCount=5  LaunchCount=1  
WarmupCount=3  

 Method                          | Mean        | Error     | StdDev   | Ratio | Rank | Gen0   | Allocated | Alloc Ratio |
-------------------------------- |------------:|----------:|---------:|------:|-----:|-------:|----------:|------------:|
 Reflection_AssemblyListProvided | 11,523.4 ns | 215.25 ns | 55.90 ns |  1.00 |    2 | 0.1678 |    3005 B |        1.00 |
 SourceGen_AssemblyListProvided  | 24,106.1 ns | 229.01 ns | 35.44 ns |  2.09 |    3 | 1.5564 |   26528 B |        8.83 |
 SourceGen_EmptyAssemblyList     | 23,476.1 ns | 230.00 ns | 59.73 ns |  2.04 |    3 | 1.5259 |   25528 B |        8.50 |
 SourceGen_ParameterlessOverload |    358.5 ns |   6.22 ns |  1.62 ns |  0.03 |    1 | 0.0262 |     440 B |        0.15 |
