```

BenchmarkDotNet v0.14.0, Ubuntu 24.04.4 LTS (Noble Numbat)
Intel Core 7 150U, 1 CPU, 12 logical and 6 physical cores
.NET SDK 10.0.302
  [Host]   : .NET 10.0.10 (10.0.1026.32716), X64 RyuJIT AVX2
  ShortRun : .NET 10.0.10 (10.0.1026.32716), X64 RyuJIT AVX2

Job=ShortRun  IterationCount=5  LaunchCount=1  
WarmupCount=3  

```
| Method                          | Mean        | Error       | StdDev       | Ratio | RatioSD | Rank | Gen0   | Allocated | Alloc Ratio |
|-------------------------------- |------------:|------------:|-------------:|------:|--------:|-----:|-------:|----------:|------------:|
| Reflection_AssemblyListProvided | 16,199.6 ns |  9,354.8 ns |  2,429.41 ns |  1.02 |    0.20 |    2 | 0.4730 |    3028 B |        1.00 |
| SourceGen_AssemblyListProvided  | 48,031.6 ns | 57,852.7 ns | 15,024.16 ns |  3.02 |    0.97 |    3 | 4.2725 |   26872 B |        8.87 |
| SourceGen_EmptyAssemblyList     | 42,235.9 ns | 55,374.0 ns | 14,380.46 ns |  2.66 |    0.91 |    3 | 4.0894 |   25864 B |        8.54 |
| SourceGen_ParameterlessOverload |    717.4 ns |    180.2 ns |     46.81 ns |  0.05 |    0.01 |    1 | 0.0696 |     440 B |        0.15 |
