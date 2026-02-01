```

BenchmarkDotNet v0.14.0, Ubuntu 24.04.3 LTS (Noble Numbat)
Intel Xeon Platinum 8370C CPU 2.80GHz, 1 CPU, 4 logical and 2 physical cores
.NET SDK 10.0.102
  [Host]   : .NET 10.0.2 (10.0.225.61305), X64 RyuJIT AVX-512F+CD+BW+DQ+VL+VBMI
  ShortRun : .NET 10.0.2 (10.0.225.61305), X64 RyuJIT AVX-512F+CD+BW+DQ+VL+VBMI

Job=ShortRun  IterationCount=5  LaunchCount=1  
WarmupCount=3  

```
| Method     | Mean     | Error    | StdDev   | Ratio | Rank | Allocated | Alloc Ratio |
|----------- |---------:|---------:|---------:|------:|-----:|----------:|------------:|
| Reflection | 14.38 ns | 0.051 ns | 0.008 ns |  1.00 |    1 |         - |          NA |
| SourceGen  | 27.23 ns | 0.185 ns | 0.029 ns |  1.89 |    2 |         - |          NA |
