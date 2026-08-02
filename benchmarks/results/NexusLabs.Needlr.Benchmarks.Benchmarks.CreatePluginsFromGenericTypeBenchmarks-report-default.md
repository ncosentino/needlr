
BenchmarkDotNet v0.14.0, Ubuntu 24.04.4 LTS (Noble Numbat)
12th Gen Intel Core i7-12700H, 1 CPU, 20 logical and 10 physical cores
.NET SDK 10.0.302
  [Host]   : .NET 10.0.10 (10.0.1026.32716), X64 RyuJIT AVX2
  ShortRun : .NET 10.0.10 (10.0.1026.32716), X64 RyuJIT AVX2

Job=ShortRun  IterationCount=5  LaunchCount=1  
WarmupCount=3  

 Method                          | Mean        | Error       | StdDev    | Ratio | RatioSD | Rank | Gen0   | Allocated | Alloc Ratio |
-------------------------------- |------------:|------------:|----------:|------:|--------:|-----:|-------:|----------:|------------:|
 Reflection_AssemblyListProvided |  7,882.5 ns | 1,670.74 ns | 433.89 ns |  1.00 |    0.07 |    2 | 0.2289 |    3010 B |        1.00 |
 SourceGen_AssemblyListProvided  | 21,331.1 ns | 3,104.38 ns | 480.41 ns |  2.71 |    0.15 |    3 | 2.1362 |   26872 B |        8.93 |
 SourceGen_EmptyAssemblyList     | 18,395.0 ns |   881.05 ns | 228.81 ns |  2.34 |    0.12 |    3 | 2.0447 |   25864 B |        8.59 |
 SourceGen_ParameterlessOverload |    411.2 ns |    95.68 ns |  24.85 ns |  0.05 |    0.00 |    1 | 0.0348 |     440 B |        0.15 |
