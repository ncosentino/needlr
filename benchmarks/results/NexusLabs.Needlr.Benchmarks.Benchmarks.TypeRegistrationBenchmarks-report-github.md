```

BenchmarkDotNet v0.14.0, Ubuntu 24.04.4 LTS (Noble Numbat)
Intel Core 7 150U, 1 CPU, 12 logical and 6 physical cores
.NET SDK 10.0.302
  [Host]   : .NET 10.0.10 (10.0.1026.32716), X64 RyuJIT AVX2
  ShortRun : .NET 10.0.10 (10.0.1026.32716), X64 RyuJIT AVX2

Job=ShortRun  IterationCount=5  LaunchCount=1  
WarmupCount=3  

```
| Method                          | Mean          | Error         | StdDev        | Ratio     | RatioSD  | Rank | Gen0    | Gen1   | Allocated | Alloc Ratio |
|-------------------------------- |--------------:|--------------:|--------------:|----------:|---------:|-----:|--------:|-------:|----------:|------------:|
| ManualDI_RegisterTypes          |      90.18 ns |      41.21 ns |      10.70 ns |      1.01 |     0.15 |    1 |  0.0842 | 0.0001 |     528 B |        1.00 |
| Needlr_Reflection_RegisterTypes | 983,032.47 ns | 442,662.20 ns | 114,957.97 ns | 11,017.11 | 1,627.71 |    3 | 93.7500 | 7.8125 |  598472 B |    1,133.47 |
| Needlr_SourceGen_RegisterTypes  |  11,988.64 ns |   5,701.50 ns |     882.31 ns |    134.36 |    16.32 |    2 |  4.9286 | 0.3510 |   30960 B |       58.64 |
