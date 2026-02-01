```

BenchmarkDotNet v0.14.0, Ubuntu 24.04.3 LTS (Noble Numbat)
Intel Xeon Platinum 8370C CPU 2.80GHz, 1 CPU, 4 logical and 2 physical cores
.NET SDK 10.0.102
  [Host]   : .NET 10.0.2 (10.0.225.61305), X64 RyuJIT AVX-512F+CD+BW+DQ+VL+VBMI
  ShortRun : .NET 10.0.2 (10.0.225.61305), X64 RyuJIT AVX-512F+CD+BW+DQ+VL+VBMI

Job=ShortRun  InvocationCount=1  IterationCount=5  
LaunchCount=1  UnrollFactor=1  WarmupCount=3  

```
| Method                      | Mean     | Error     | StdDev    | Ratio | RatioSD | Rank | Allocated  | Alloc Ratio |
|---------------------------- |---------:|----------:|----------:|------:|--------:|-----:|-----------:|------------:|
| BuildHost_Reflection        | 5.482 ms | 0.2770 ms | 0.0429 ms |  1.00 |    0.01 |    2 | 1019.38 KB |        1.00 |
| BuildHost_SourceGen         | 2.294 ms | 0.3790 ms | 0.0984 ms |  0.42 |    0.02 |    1 |  503.33 KB |        0.49 |
| BuildHost_SourceGenExplicit | 2.281 ms | 0.2243 ms | 0.0583 ms |  0.42 |    0.01 |    1 |  445.72 KB |        0.44 |
