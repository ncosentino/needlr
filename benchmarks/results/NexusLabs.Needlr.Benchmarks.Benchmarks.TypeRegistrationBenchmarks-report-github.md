```

BenchmarkDotNet v0.14.0, Ubuntu 24.04.3 LTS (Noble Numbat)
AMD EPYC 7763, 1 CPU, 4 logical and 2 physical cores
.NET SDK 10.0.102
  [Host]   : .NET 10.0.2 (10.0.225.61305), X64 RyuJIT AVX2
  ShortRun : .NET 10.0.2 (10.0.225.61305), X64 RyuJIT AVX2

Job=ShortRun  IterationCount=5  LaunchCount=1  
WarmupCount=3  

```
| Method                   | Mean      | Error    | StdDev   | Ratio | Rank | Gen0    | Gen1   | Allocated | Alloc Ratio |
|------------------------- |----------:|---------:|---------:|------:|-----:|--------:|-------:|----------:|------------:|
| RegisterTypes_Reflection | 929.31 μs | 9.674 μs | 2.512 μs |  1.00 |    2 | 27.3438 |      - | 463.83 KB |        1.00 |
| RegisterTypes_SourceGen  |  16.33 μs | 0.211 μs | 0.055 μs |  0.02 |    1 |  1.6785 | 0.1221 |   27.6 KB |        0.06 |
