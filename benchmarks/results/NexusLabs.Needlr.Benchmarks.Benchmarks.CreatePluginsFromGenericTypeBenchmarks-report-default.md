
BenchmarkDotNet v0.14.0, Ubuntu 24.04.4 LTS (Noble Numbat)
AMD EPYC 9V74, 1 CPU, 4 logical and 2 physical cores
.NET SDK 10.0.300
  [Host]   : .NET 10.0.8 (10.0.826.23019), X64 RyuJIT AVX2
  ShortRun : .NET 10.0.8 (10.0.826.23019), X64 RyuJIT AVX2

Job=ShortRun  IterationCount=5  LaunchCount=1  
WarmupCount=3  

 Method                          | Mean        | Error     | StdDev   | Ratio | Rank | Gen0   | Allocated | Alloc Ratio |
-------------------------------- |------------:|----------:|---------:|------:|-----:|-------:|----------:|------------:|
 Reflection_AssemblyListProvided | 11,400.8 ns |  86.05 ns | 22.35 ns |  1.00 |    2 | 0.1678 |    3005 B |        1.00 |
 SourceGen_AssemblyListProvided  | 24,852.4 ns | 351.58 ns | 91.30 ns |  2.18 |    3 | 1.5564 |   26528 B |        8.83 |
 SourceGen_EmptyAssemblyList     | 23,495.7 ns | 216.78 ns | 33.55 ns |  2.06 |    3 | 1.5259 |   25528 B |        8.50 |
 SourceGen_ParameterlessOverload |    448.7 ns |   1.77 ns |  0.46 ns |  0.04 |    1 | 0.0262 |     440 B |        0.15 |
