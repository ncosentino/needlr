
BenchmarkDotNet v0.14.0, Ubuntu 24.04.3 LTS (Noble Numbat)
Intel Xeon Platinum 8370C CPU 2.80GHz, 1 CPU, 4 logical and 2 physical cores
.NET SDK 10.0.102
  [Host]   : .NET 10.0.2 (10.0.225.61305), X64 RyuJIT AVX-512F+CD+BW+DQ+VL+VBMI
  ShortRun : .NET 10.0.2 (10.0.225.61305), X64 RyuJIT AVX-512F+CD+BW+DQ+VL+VBMI

Job=ShortRun  IterationCount=5  LaunchCount=1  
WarmupCount=3  

 Method     | Mean     | Error    | StdDev   | Ratio | RatioSD | Rank | Allocated | Alloc Ratio |
----------- |---------:|---------:|---------:|------:|--------:|-----:|----------:|------------:|
 Reflection | 12.66 ns | 0.168 ns | 0.044 ns |  1.00 |    0.00 |    1 |         - |          NA |
 SourceGen  | 13.91 ns | 0.851 ns | 0.221 ns |  1.10 |    0.02 |    1 |         - |          NA |
