```

BenchmarkDotNet v0.14.0, Ubuntu 24.04.3 LTS (Noble Numbat)
Intel Xeon Platinum 8370C CPU 2.80GHz, 1 CPU, 4 logical and 2 physical cores
.NET SDK 10.0.102
  [Host]   : .NET 10.0.2 (10.0.225.61305), X64 RyuJIT AVX-512F+CD+BW+DQ+VL+VBMI
  ShortRun : .NET 10.0.2 (10.0.225.61305), X64 RyuJIT AVX-512F+CD+BW+DQ+VL+VBMI

Job=ShortRun  IterationCount=5  LaunchCount=1  
WarmupCount=3  

```
| Method                          | Mean        | Error     | StdDev   | Ratio | Rank | Gen0   | Allocated | Alloc Ratio |
|-------------------------------- |------------:|----------:|---------:|------:|-----:|-------:|----------:|------------:|
| Reflection_AssemblyListProvided | 19,775.9 ns | 122.28 ns | 18.92 ns |  1.00 |    2 | 0.1831 |    4714 B |        1.00 |
| SourceGen_AssemblyListProvided  | 20,409.1 ns | 259.02 ns | 67.27 ns |  1.03 |    2 | 0.7629 |   19328 B |        4.10 |
| SourceGen_EmptyAssemblyList     | 18,765.3 ns | 209.51 ns | 32.42 ns |  0.95 |    2 | 0.7019 |   18328 B |        3.89 |
| SourceGen_ParameterlessOverload |    301.8 ns |   3.58 ns |  0.55 ns |  0.02 |    1 | 0.0172 |     440 B |        0.09 |
