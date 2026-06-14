```

BenchmarkDotNet v0.14.0, Ubuntu 24.04.4 LTS (Noble Numbat)
AMD EPYC 7763, 1 CPU, 4 logical and 2 physical cores
.NET SDK 10.0.301
  [Host]   : .NET 10.0.9 (10.0.926.27113), X64 RyuJIT AVX2
  ShortRun : .NET 10.0.9 (10.0.926.27113), X64 RyuJIT AVX2

Job=ShortRun  IterationCount=5  LaunchCount=1  
WarmupCount=3  

```
| Method                          | Mean        | Error     | StdDev   | Ratio | Rank | Gen0   | Allocated | Alloc Ratio |
|-------------------------------- |------------:|----------:|---------:|------:|-----:|-------:|----------:|------------:|
| Reflection_AssemblyListProvided | 11,720.1 ns | 229.63 ns | 59.63 ns |  1.00 |    2 | 0.1678 |    3005 B |        1.00 |
| SourceGen_AssemblyListProvided  | 26,693.0 ns | 173.12 ns | 44.96 ns |  2.28 |    3 | 1.5564 |   26528 B |        8.83 |
| SourceGen_EmptyAssemblyList     | 24,569.0 ns | 208.97 ns | 32.34 ns |  2.10 |    3 | 1.5259 |   25528 B |        8.50 |
| SourceGen_ParameterlessOverload |    474.8 ns |   6.48 ns |  1.00 ns |  0.04 |    1 | 0.0257 |     440 B |        0.15 |
