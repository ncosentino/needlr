
BenchmarkDotNet v0.14.0, Ubuntu 24.04.3 LTS (Noble Numbat)
Intel Xeon Platinum 8370C CPU 2.80GHz, 1 CPU, 4 logical and 2 physical cores
.NET SDK 10.0.102
  [Host]   : .NET 10.0.2 (10.0.225.61305), X64 RyuJIT AVX-512F+CD+BW+DQ+VL+VBMI
  ShortRun : .NET 10.0.2 (10.0.225.61305), X64 RyuJIT AVX-512F+CD+BW+DQ+VL+VBMI

Job=ShortRun  InvocationCount=1  IterationCount=5  
LaunchCount=1  UnrollFactor=1  WarmupCount=3  

 Method            | Mean     | Error     | StdDev    | Ratio | RatioSD | Rank | Allocated  | Alloc Ratio |
------------------ |---------:|----------:|----------:|------:|--------:|-----:|-----------:|------------:|
 Reflection        | 6.260 ms | 0.3787 ms | 0.0984 ms |  1.00 |    0.02 |    2 | 1213.04 KB |        1.00 |
 SourceGen         | 2.879 ms | 0.0756 ms | 0.0117 ms |  0.46 |    0.01 |    1 |  695.52 KB |        0.57 |
 SourceGenExplicit | 2.833 ms | 0.2570 ms | 0.0667 ms |  0.45 |    0.01 |    1 |  648.27 KB |        0.53 |
