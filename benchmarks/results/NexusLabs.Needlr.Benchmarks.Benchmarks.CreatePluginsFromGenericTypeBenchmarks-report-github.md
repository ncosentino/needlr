```

BenchmarkDotNet v0.14.0, Ubuntu 24.04.3 LTS (Noble Numbat)
AMD EPYC 7763, 1 CPU, 4 logical and 2 physical cores
.NET SDK 10.0.102
  [Host]   : .NET 10.0.2 (10.0.225.61305), X64 RyuJIT AVX2
  ShortRun : .NET 10.0.2 (10.0.225.61305), X64 RyuJIT AVX2

Job=ShortRun  IterationCount=5  LaunchCount=1  
WarmupCount=3  

```
| Method                          | Mean        | Error       | StdDev    | Ratio | Rank | Gen0   | Allocated | Alloc Ratio |
|-------------------------------- |------------:|------------:|----------:|------:|-----:|-------:|----------:|------------:|
| Reflection_AssemblyListProvided | 30,726.5 ns |   818.35 ns | 126.64 ns |  1.00 |    3 | 0.3662 |    6552 B |        1.00 |
| SourceGen_AssemblyListProvided  | 22,989.0 ns |   614.83 ns |  95.15 ns |  0.75 |    2 | 1.4038 |   23528 B |        3.59 |
| SourceGen_EmptyAssemblyList     | 21,855.5 ns | 1,498.80 ns | 389.23 ns |  0.71 |    2 | 1.3428 |   22528 B |        3.44 |
| SourceGen_ParameterlessOverload |    439.2 ns |    12.63 ns |   3.28 ns |  0.01 |    1 | 0.0262 |     440 B |        0.07 |
