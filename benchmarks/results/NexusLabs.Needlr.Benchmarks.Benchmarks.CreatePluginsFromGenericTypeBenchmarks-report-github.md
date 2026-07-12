```

BenchmarkDotNet v0.14.0, Ubuntu 24.04.4 LTS (Noble Numbat)
AMD EPYC 7763, 1 CPU, 4 logical and 2 physical cores
.NET SDK 10.0.301
  [Host]   : .NET 10.0.9 (10.0.926.27113), X64 RyuJIT AVX2
  ShortRun : .NET 10.0.9 (10.0.926.27113), X64 RyuJIT AVX2

Job=ShortRun  IterationCount=5  LaunchCount=1  
WarmupCount=3  

```
| Method                          | Mean        | Error     | StdDev    | Ratio | RatioSD | Rank | Gen0   | Allocated | Alloc Ratio |
|-------------------------------- |------------:|----------:|----------:|------:|--------:|-----:|-------:|----------:|------------:|
| Reflection_AssemblyListProvided | 11,948.3 ns | 488.38 ns | 126.83 ns |  1.00 |    0.01 |    2 | 0.1678 |    3005 B |        1.00 |
| SourceGen_AssemblyListProvided  | 26,629.3 ns | 815.07 ns | 211.67 ns |  2.23 |    0.03 |    3 | 1.5564 |   26528 B |        8.83 |
| SourceGen_EmptyAssemblyList     | 25,755.4 ns | 264.09 ns |  68.58 ns |  2.16 |    0.02 |    3 | 1.5259 |   25528 B |        8.50 |
| SourceGen_ParameterlessOverload |    461.2 ns |   5.89 ns |   1.53 ns |  0.04 |    0.00 |    1 | 0.0262 |     440 B |        0.15 |
