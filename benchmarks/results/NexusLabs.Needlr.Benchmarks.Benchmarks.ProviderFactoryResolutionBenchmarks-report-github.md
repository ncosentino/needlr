```

BenchmarkDotNet v0.14.0, Ubuntu 24.04.4 LTS (Noble Numbat)
AMD EPYC 9V74, 1 CPU, 4 logical and 2 physical cores
.NET SDK 10.0.302
  [Host]   : .NET 10.0.10 (10.0.1026.32716), X64 RyuJIT AVX2
  ShortRun : .NET 10.0.10 (10.0.1026.32716), X64 RyuJIT AVX2

Job=ShortRun  IterationCount=5  LaunchCount=1  
WarmupCount=3  

```
| Method                              | Mean      | Error     | StdDev    | Ratio | RatioSD | Rank | Gen0   | Allocated | Alloc Ratio |
|------------------------------------ |----------:|----------:|----------:|------:|--------:|-----:|-------:|----------:|------------:|
| ManualDI_FuncFactory_PreResolved    |  9.171 ns | 0.7466 ns | 0.1155 ns |  1.00 |    0.02 |    1 | 0.0019 |      32 B |        1.00 |
| ManualDI_FuncFactory_WithResolution | 23.722 ns | 0.1793 ns | 0.0277 ns |  2.59 |    0.03 |    2 | 0.0019 |      32 B |        1.00 |
| DirectFactory_PreResolved           |  8.592 ns | 0.3681 ns | 0.0570 ns |  0.94 |    0.01 |    1 | 0.0019 |      32 B |        1.00 |
| DirectFactory_WithResolution        | 26.723 ns | 1.5018 ns | 0.2324 ns |  2.91 |    0.04 |    2 | 0.0019 |      32 B |        1.00 |
| Provider_FactoryShorthand           |  8.895 ns | 0.8529 ns | 0.2215 ns |  0.97 |    0.02 |    1 | 0.0019 |      32 B |        1.00 |
| Provider_FactoryInterface           |  9.064 ns | 1.9176 ns | 0.4980 ns |  0.99 |    0.05 |    1 | 0.0019 |      32 B |        1.00 |
| Provider_MixedShorthand             |  8.906 ns | 0.4660 ns | 0.1210 ns |  0.97 |    0.02 |    1 | 0.0019 |      32 B |        1.00 |
