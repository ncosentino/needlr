```

BenchmarkDotNet v0.14.0, Ubuntu 24.04.4 LTS (Noble Numbat)
AMD EPYC 9V74, 1 CPU, 4 logical and 2 physical cores
.NET SDK 10.0.201
  [Host]   : .NET 10.0.5 (10.0.526.15411), X64 RyuJIT AVX2
  ShortRun : .NET 10.0.5 (10.0.526.15411), X64 RyuJIT AVX2

Job=ShortRun  IterationCount=5  LaunchCount=1  
WarmupCount=3  

```
| Method                          | Mean        | Error     | StdDev    | Ratio | RatioSD | Rank | Gen0   | Allocated | Alloc Ratio |
|-------------------------------- |------------:|----------:|----------:|------:|--------:|-----:|-------:|----------:|------------:|
| Reflection_AssemblyListProvided | 11,580.1 ns | 303.03 ns |  46.89 ns |  1.00 |    0.01 |    2 | 0.1678 |    3005 B |        1.00 |
| SourceGen_AssemblyListProvided  | 24,838.5 ns | 752.67 ns | 195.47 ns |  2.14 |    0.02 |    3 | 1.5564 |   26528 B |        8.83 |
| SourceGen_EmptyAssemblyList     | 22,957.3 ns | 148.60 ns |  38.59 ns |  1.98 |    0.01 |    3 | 1.5259 |   25528 B |        8.50 |
| SourceGen_ParameterlessOverload |    374.2 ns |   9.86 ns |   2.56 ns |  0.03 |    0.00 |    1 | 0.0262 |     440 B |        0.15 |
