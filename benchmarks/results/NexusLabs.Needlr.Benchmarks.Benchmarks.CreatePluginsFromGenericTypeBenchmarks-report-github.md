```

BenchmarkDotNet v0.14.0, Ubuntu 24.04.4 LTS (Noble Numbat)
AMD EPYC 7763, 1 CPU, 4 logical and 2 physical cores
.NET SDK 10.0.301
  [Host]   : .NET 10.0.9 (10.0.926.27113), X64 RyuJIT AVX2
  ShortRun : .NET 10.0.9 (10.0.926.27113), X64 RyuJIT AVX2

Job=ShortRun  IterationCount=5  LaunchCount=1  
WarmupCount=3  

```
| Method                          | Mean        | Error     | StdDev    | Ratio | Rank | Gen0   | Allocated | Alloc Ratio |
|-------------------------------- |------------:|----------:|----------:|------:|-----:|-------:|----------:|------------:|
| Reflection_AssemblyListProvided | 11,987.9 ns | 165.64 ns |  43.02 ns |  1.00 |    2 | 0.1678 |    3005 B |        1.00 |
| SourceGen_AssemblyListProvided  | 25,483.4 ns | 564.77 ns | 146.67 ns |  2.13 |    3 | 1.5564 |   26528 B |        8.83 |
| SourceGen_EmptyAssemblyList     | 25,153.4 ns | 476.16 ns | 123.66 ns |  2.10 |    3 | 1.5259 |   25528 B |        8.50 |
| SourceGen_ParameterlessOverload |    478.6 ns |  19.36 ns |   5.03 ns |  0.04 |    1 | 0.0257 |     440 B |        0.15 |
