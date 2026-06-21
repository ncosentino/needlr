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
| Reflection_AssemblyListProvided | 11,777.4 ns | 465.47 ns | 120.88 ns |  1.00 |    0.01 |    2 | 0.1678 |    3005 B |        1.00 |
| SourceGen_AssemblyListProvided  | 26,472.6 ns | 558.07 ns | 144.93 ns |  2.25 |    0.02 |    3 | 1.5564 |   26528 B |        8.83 |
| SourceGen_EmptyAssemblyList     | 24,811.3 ns | 450.97 ns | 117.12 ns |  2.11 |    0.02 |    3 | 1.5259 |   25528 B |        8.50 |
| SourceGen_ParameterlessOverload |    479.0 ns |   7.17 ns |   1.86 ns |  0.04 |    0.00 |    1 | 0.0257 |     440 B |        0.15 |
