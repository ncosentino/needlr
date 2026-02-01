```

BenchmarkDotNet v0.14.0, Ubuntu 24.04.3 LTS (Noble Numbat)
AMD EPYC 7763, 1 CPU, 4 logical and 2 physical cores
.NET SDK 10.0.102
  [Host]   : .NET 10.0.2 (10.0.225.61305), X64 RyuJIT AVX2
  ShortRun : .NET 10.0.2 (10.0.225.61305), X64 RyuJIT AVX2

Job=ShortRun  IterationCount=5  LaunchCount=1  
WarmupCount=3  

```
| Method                          | Mean        | Error     | StdDev   | Ratio | Rank | Gen0   | Allocated | Alloc Ratio |
|-------------------------------- |------------:|----------:|---------:|------:|-----:|-------:|----------:|------------:|
| Reflection_AssemblyListProvided | 25,786.0 ns | 520.02 ns | 80.47 ns |  1.00 |    2 | 0.2441 |    5662 B |        1.00 |
| SourceGen_AssemblyListProvided  | 21,544.8 ns |  76.44 ns | 19.85 ns |  0.84 |    2 | 1.2817 |   21728 B |        3.84 |
| SourceGen_EmptyAssemblyList     | 19,534.7 ns | 139.78 ns | 36.30 ns |  0.76 |    2 | 1.2207 |   20728 B |        3.66 |
| SourceGen_ParameterlessOverload |    417.8 ns |   9.21 ns |  2.39 ns |  0.02 |    1 | 0.0262 |     440 B |        0.08 |
