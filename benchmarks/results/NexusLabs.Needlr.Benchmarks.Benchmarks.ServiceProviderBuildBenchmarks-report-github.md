```

BenchmarkDotNet v0.14.0, Ubuntu 24.04.5 LTS (Noble Numbat)
AMD EPYC 9V74, 1 CPU, 4 logical and 2 physical cores
.NET SDK 10.0.303
  [Host]   : .NET 10.0.11 (10.0.1126.37416), X64 RyuJIT AVX-512F+CD+BW+DQ+VL+VBMI
  ShortRun : .NET 10.0.11 (10.0.1126.37416), X64 RyuJIT AVX-512F+CD+BW+DQ+VL+VBMI

Job=ShortRun  IterationCount=5  LaunchCount=1  
WarmupCount=3  

```
| Method                                        | Mean           | Error           | StdDev        | Ratio    | RatioSD | Rank | Gen0    | Gen1   | Allocated | Alloc Ratio |
|---------------------------------------------- |---------------:|----------------:|--------------:|---------:|--------:|-----:|--------:|-------:|----------:|------------:|
| ManualDI_BuildServiceProvider                 |       962.1 ns |        48.34 ns |       7.48 ns |     1.00 |    0.01 |    1 |  0.2785 | 0.0916 |   4.56 KB |        1.00 |
| Needlr_Reflection_BuildServiceProvider        | 1,971,041.0 ns | 2,324,787.58 ns | 359,763.45 ns | 2,048.75 |  334.78 |    4 | 54.6875 | 7.8125 | 923.89 KB |      202.50 |
| Needlr_SourceGenExplicit_BuildServiceProvider |    58,840.8 ns |     5,528.07 ns |   1,435.62 ns |    61.16 |    1.43 |    2 |  6.3477 | 0.4883 | 107.04 KB |       23.46 |
| Needlr_SourceGenImplicit_BuildServiceProvider |    75,610.4 ns |     9,210.20 ns |   1,425.29 ns |    78.59 |    1.43 |    3 | 11.2305 | 1.4648 | 184.08 KB |       40.35 |
