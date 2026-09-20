```

BenchmarkDotNet v0.14.0, Ubuntu 24.04.5 LTS (Noble Numbat)
AMD EPYC 9V74, 1 CPU, 4 logical and 2 physical cores
.NET SDK 10.0.303
  [Host]   : .NET 10.0.11 (10.0.1126.37416), X64 RyuJIT AVX-512F+CD+BW+DQ+VL+VBMI
  ShortRun : .NET 10.0.11 (10.0.1126.37416), X64 RyuJIT AVX-512F+CD+BW+DQ+VL+VBMI

Job=ShortRun  IterationCount=5  LaunchCount=1  
WarmupCount=3  

```
| Method                          | Mean        | Error     | StdDev   | Ratio | RatioSD | Rank | Gen0   | Allocated | Alloc Ratio |
|-------------------------------- |------------:|----------:|---------:|------:|--------:|-----:|-------:|----------:|------------:|
| Reflection_AssemblyListProvided |  9,136.5 ns | 244.02 ns | 63.37 ns |  1.00 |    0.01 |    2 | 0.1678 |    3005 B |        1.00 |
| SourceGen_AssemblyListProvided  | 19,211.5 ns | 260.40 ns | 67.62 ns |  2.10 |    0.01 |    3 | 1.5564 |   26528 B |        8.83 |
| SourceGen_EmptyAssemblyList     | 18,195.5 ns | 558.88 ns | 86.49 ns |  1.99 |    0.02 |    3 | 1.5259 |   25528 B |        8.50 |
| SourceGen_ParameterlessOverload |    278.8 ns |   4.57 ns |  0.71 ns |  0.03 |    0.00 |    1 | 0.0262 |     440 B |        0.15 |
