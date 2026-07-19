```

BenchmarkDotNet v0.14.0, Ubuntu 24.04.4 LTS (Noble Numbat)
AMD EPYC 9V74, 1 CPU, 4 logical and 2 physical cores
.NET SDK 10.0.302
  [Host]   : .NET 10.0.10 (10.0.1026.32716), X64 RyuJIT AVX2
  ShortRun : .NET 10.0.10 (10.0.1026.32716), X64 RyuJIT AVX2

Job=ShortRun  IterationCount=5  LaunchCount=1  
WarmupCount=3  

```
| Method                          | Mean        | Error     | StdDev   | Ratio | Rank | Gen0   | Allocated | Alloc Ratio |
|-------------------------------- |------------:|----------:|---------:|------:|-----:|-------:|----------:|------------:|
| Reflection_AssemblyListProvided | 11,621.4 ns | 204.59 ns | 53.13 ns |  1.00 |    2 | 0.1678 |    3005 B |        1.00 |
| SourceGen_AssemblyListProvided  | 24,142.5 ns | 209.39 ns | 54.38 ns |  2.08 |    3 | 1.5564 |   26528 B |        8.83 |
| SourceGen_EmptyAssemblyList     | 23,652.2 ns | 296.08 ns | 76.89 ns |  2.04 |    3 | 1.5259 |   25528 B |        8.50 |
| SourceGen_ParameterlessOverload |    351.0 ns |   2.88 ns |  0.45 ns |  0.03 |    1 | 0.0262 |     440 B |        0.15 |
