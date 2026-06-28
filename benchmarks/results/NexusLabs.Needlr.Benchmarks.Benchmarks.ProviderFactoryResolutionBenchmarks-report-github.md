```

BenchmarkDotNet v0.14.0, Ubuntu 24.04.4 LTS (Noble Numbat)
AMD EPYC 7763, 1 CPU, 4 logical and 2 physical cores
.NET SDK 10.0.301
  [Host]   : .NET 10.0.9 (10.0.926.27113), X64 RyuJIT AVX2
  ShortRun : .NET 10.0.9 (10.0.926.27113), X64 RyuJIT AVX2

Job=ShortRun  IterationCount=5  LaunchCount=1  
WarmupCount=3  

```
| Method                              | Mean      | Error     | StdDev    | Ratio | RatioSD | Rank | Gen0   | Allocated | Alloc Ratio |
|------------------------------------ |----------:|----------:|----------:|------:|--------:|-----:|-------:|----------:|------------:|
| ManualDI_FuncFactory_PreResolved    |  8.843 ns | 0.2633 ns | 0.0407 ns |  1.00 |    0.01 |    1 | 0.0019 |      32 B |        1.00 |
| ManualDI_FuncFactory_WithResolution | 19.563 ns | 0.3263 ns | 0.0505 ns |  2.21 |    0.01 |    2 | 0.0019 |      32 B |        1.00 |
| DirectFactory_PreResolved           | 10.087 ns | 1.8640 ns | 0.4841 ns |  1.14 |    0.05 |    1 | 0.0019 |      32 B |        1.00 |
| DirectFactory_WithResolution        | 22.820 ns | 0.1490 ns | 0.0231 ns |  2.58 |    0.01 |    2 | 0.0019 |      32 B |        1.00 |
| Provider_FactoryShorthand           |  7.923 ns | 0.2280 ns | 0.0353 ns |  0.90 |    0.01 |    1 | 0.0019 |      32 B |        1.00 |
| Provider_FactoryInterface           | 19.920 ns | 1.8507 ns | 0.4806 ns |  2.25 |    0.05 |    2 | 0.0019 |      32 B |        1.00 |
| Provider_MixedShorthand             | 16.380 ns | 1.5895 ns | 0.4128 ns |  1.85 |    0.04 |    2 | 0.0019 |      32 B |        1.00 |
