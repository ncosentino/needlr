```

BenchmarkDotNet v0.14.0, Ubuntu 24.04.5 LTS (Noble Numbat)
AMD EPYC 7763, 1 CPU, 4 logical and 2 physical cores
.NET SDK 10.0.404
  [Host]   : .NET 10.0.15 (10.0.1526.53008), X64 RyuJIT AVX2
  ShortRun : .NET 10.0.15 (10.0.1526.53008), X64 RyuJIT AVX2

Job=ShortRun  IterationCount=5  LaunchCount=1  
WarmupCount=3  

```
| Method                          | Mean        | Error     | StdDev   | Ratio | RatioSD | Rank | Gen0   | Allocated | Alloc Ratio |
|-------------------------------- |------------:|----------:|---------:|------:|--------:|-----:|-------:|----------:|------------:|
| Reflection_AssemblyListProvided | 12,059.8 ns | 371.99 ns | 96.60 ns |  1.00 |    0.01 |    2 | 0.1678 |    3005 B |        1.00 |
| SourceGen_AssemblyListProvided  | 27,222.0 ns | 253.48 ns | 39.23 ns |  2.26 |    0.02 |    3 | 1.5564 |   26528 B |        8.83 |
| SourceGen_EmptyAssemblyList     | 26,068.4 ns | 260.17 ns | 40.26 ns |  2.16 |    0.02 |    3 | 1.5259 |   25528 B |        8.50 |
| SourceGen_ParameterlessOverload |    497.2 ns |  12.29 ns |  3.19 ns |  0.04 |    0.00 |    1 | 0.0257 |     440 B |        0.15 |
