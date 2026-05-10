```

BenchmarkDotNet v0.14.0, Ubuntu 24.04.4 LTS (Noble Numbat)
AMD EPYC 9V74, 1 CPU, 4 logical and 2 physical cores
.NET SDK 10.0.203
  [Host]   : .NET 10.0.7 (10.0.726.21808), X64 RyuJIT AVX2
  ShortRun : .NET 10.0.7 (10.0.726.21808), X64 RyuJIT AVX2

Job=ShortRun  IterationCount=5  LaunchCount=1  
WarmupCount=3  

```
| Method                          | Mean        | Error    | StdDev    | Ratio | Rank | Gen0   | Allocated | Alloc Ratio |
|-------------------------------- |------------:|---------:|----------:|------:|-----:|-------:|----------:|------------:|
| Reflection_AssemblyListProvided | 11,421.5 ns | 163.4 ns |  42.44 ns |  1.00 |    2 | 0.1678 |    3005 B |        1.00 |
| SourceGen_AssemblyListProvided  | 24,506.0 ns | 137.9 ns |  35.81 ns |  2.15 |    3 | 1.5564 |   26528 B |        8.83 |
| SourceGen_EmptyAssemblyList     | 23,618.5 ns | 240.4 ns |  62.43 ns |  2.07 |    3 | 1.5259 |   25528 B |        8.50 |
| SourceGen_ParameterlessOverload |    647.2 ns | 628.7 ns | 163.27 ns |  0.06 |    1 | 0.0262 |     440 B |        0.15 |
