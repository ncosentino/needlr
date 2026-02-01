```

BenchmarkDotNet v0.14.0, Ubuntu 24.04.3 LTS (Noble Numbat)
AMD EPYC 7763, 1 CPU, 4 logical and 2 physical cores
.NET SDK 10.0.102
  [Host]   : .NET 10.0.2 (10.0.225.61305), X64 RyuJIT AVX2
  ShortRun : .NET 10.0.2 (10.0.225.61305), X64 RyuJIT AVX2

Job=ShortRun  IterationCount=5  LaunchCount=1  
WarmupCount=3  

```
| Method                          | Mean        | Error     | StdDev    | Ratio | Rank | Gen0   | Allocated | Alloc Ratio |
|-------------------------------- |------------:|----------:|----------:|------:|-----:|-------:|----------:|------------:|
| Reflection                      | 20,571.8 ns | 300.84 ns |  78.13 ns |  1.00 |    2 | 0.2747 |    4719 B |        1.00 |
| SourceGen_AssemblyListProvided  | 19,637.5 ns | 538.03 ns | 139.73 ns |  0.95 |    2 | 1.1292 |   19328 B |        4.10 |
| SourceGen_EmptyAssemblyList     | 18,372.5 ns | 206.86 ns |  32.01 ns |  0.89 |    2 | 1.0681 |   18328 B |        3.88 |
| SourceGen_ParameterlessOverload |    410.6 ns |   7.06 ns |   1.83 ns |  0.02 |    1 | 0.0262 |     440 B |        0.09 |
