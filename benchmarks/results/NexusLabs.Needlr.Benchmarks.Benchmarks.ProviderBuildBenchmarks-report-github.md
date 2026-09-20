```

BenchmarkDotNet v0.14.0, Ubuntu 24.04.5 LTS (Noble Numbat)
AMD EPYC 9V74, 1 CPU, 4 logical and 2 physical cores
.NET SDK 10.0.303
  [Host]   : .NET 10.0.11 (10.0.1126.37416), X64 RyuJIT AVX-512F+CD+BW+DQ+VL+VBMI
  ShortRun : .NET 10.0.11 (10.0.1126.37416), X64 RyuJIT AVX-512F+CD+BW+DQ+VL+VBMI

Job=ShortRun  IterationCount=5  LaunchCount=1  
WarmupCount=3  

```
| Method                              | Mean         | Error        | StdDev      | Ratio  | RatioSD | Rank | Gen0    | Gen1   | Allocated | Alloc Ratio |
|------------------------------------ |-------------:|-------------:|------------:|-------:|--------:|-----:|--------:|-------:|----------:|------------:|
| ManualDI_ToServiceProvider          |     903.7 ns |     32.02 ns |     8.31 ns |   1.00 |    0.01 |    1 |  0.2689 | 0.0887 |   4.41 KB |        1.00 |
| Needlr_Reflection_ToServiceProvider | 611,403.8 ns | 13,790.12 ns | 2,134.04 ns | 676.64 |    6.11 |    3 | 13.6719 |      - | 252.82 KB |       57.38 |
| Needlr_SourceGen_ToServiceProvider  |  78,088.4 ns |  2,398.46 ns |   622.87 ns |  86.42 |    0.96 |    2 | 11.2305 | 1.4648 | 184.08 KB |       41.78 |
| Needlr_SourceGen_ToProvider         |  79,518.9 ns |  3,523.00 ns |   914.91 ns |  88.00 |    1.19 |    2 | 11.2305 | 1.4648 | 188.75 KB |       42.84 |
