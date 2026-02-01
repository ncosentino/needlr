```

BenchmarkDotNet v0.14.0, Ubuntu 24.04.3 LTS (Noble Numbat)
AMD EPYC 7763, 1 CPU, 4 logical and 2 physical cores
.NET SDK 10.0.102
  [Host]   : .NET 10.0.2 (10.0.225.61305), X64 RyuJIT AVX2
  ShortRun : .NET 10.0.2 (10.0.225.61305), X64 RyuJIT AVX2

Job=ShortRun  IterationCount=5  LaunchCount=1  
WarmupCount=3  

```
| Method                   | Mean      | Error      | StdDev     | Ratio | RatioSD | Rank | Gen0    | Gen1   | Allocated | Alloc Ratio |
|------------------------- |----------:|-----------:|-----------:|------:|--------:|-----:|--------:|-------:|----------:|------------:|
| RegisterTypes_Reflection | 817.37 μs | 567.136 μs | 147.283 μs |  1.02 |    0.23 |    2 | 19.5313 |      - | 335.88 KB |        1.00 |
| RegisterTypes_SourceGen  |  14.80 μs |   0.399 μs |   0.104 μs |  0.02 |    0.00 |    1 |  1.3885 | 0.0763 |  22.85 KB |        0.07 |
