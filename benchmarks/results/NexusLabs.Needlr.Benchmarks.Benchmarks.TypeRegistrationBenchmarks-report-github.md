```

BenchmarkDotNet v0.14.0, Ubuntu 24.04.3 LTS (Noble Numbat)
Intel Xeon Platinum 8370C CPU 2.80GHz, 1 CPU, 4 logical and 2 physical cores
.NET SDK 10.0.102
  [Host]   : .NET 10.0.2 (10.0.225.61305), X64 RyuJIT AVX-512F+CD+BW+DQ+VL+VBMI
  ShortRun : .NET 10.0.2 (10.0.225.61305), X64 RyuJIT AVX-512F+CD+BW+DQ+VL+VBMI

Job=ShortRun  IterationCount=5  LaunchCount=1  
WarmupCount=3  

```
| Method                   | Mean      | Error     | StdDev    | Ratio | RatioSD | Rank | Gen0    | Gen1   | Allocated | Alloc Ratio |
|------------------------- |----------:|----------:|----------:|------:|--------:|-----:|--------:|-------:|----------:|------------:|
| RegisterTypes_Reflection | 693.35 μs | 66.177 μs | 10.241 μs |  1.00 |    0.02 |    2 | 11.7188 |      - | 335.13 KB |        1.00 |
| RegisterTypes_SourceGen  |  11.50 μs |  0.041 μs |  0.006 μs |  0.02 |    0.00 |    1 |  0.9308 | 0.0458 |  22.85 KB |        0.07 |
