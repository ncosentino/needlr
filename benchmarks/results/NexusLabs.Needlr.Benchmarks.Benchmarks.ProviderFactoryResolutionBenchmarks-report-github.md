```

BenchmarkDotNet v0.14.0, Ubuntu 24.04.4 LTS (Noble Numbat)
AMD EPYC 7763, 1 CPU, 4 logical and 2 physical cores
.NET SDK 10.0.203
  [Host]   : .NET 10.0.7 (10.0.726.21808), X64 RyuJIT AVX2
  ShortRun : .NET 10.0.7 (10.0.726.21808), X64 RyuJIT AVX2

Job=ShortRun  IterationCount=5  LaunchCount=1  
WarmupCount=3  

```
| Method                              | Mean      | Error     | StdDev    | Ratio | RatioSD | Rank | Gen0   | Allocated | Alloc Ratio |
|------------------------------------ |----------:|----------:|----------:|------:|--------:|-----:|-------:|----------:|------------:|
| ManualDI_FuncFactory_PreResolved    | 11.457 ns | 4.4791 ns | 1.1632 ns |  1.01 |    0.14 |    2 | 0.0019 |      32 B |        1.00 |
| ManualDI_FuncFactory_WithResolution | 20.234 ns | 2.4314 ns | 0.6314 ns |  1.78 |    0.19 |    3 | 0.0019 |      32 B |        1.00 |
| DirectFactory_PreResolved           |  7.538 ns | 0.0503 ns | 0.0131 ns |  0.66 |    0.07 |    1 | 0.0019 |      32 B |        1.00 |
| DirectFactory_WithResolution        | 28.213 ns | 1.3641 ns | 0.3543 ns |  2.48 |    0.25 |    4 | 0.0019 |      32 B |        1.00 |
| Provider_FactoryShorthand           | 16.291 ns | 2.4657 ns | 0.3816 ns |  1.43 |    0.15 |    3 | 0.0019 |      32 B |        1.00 |
| Provider_FactoryInterface           | 16.062 ns | 2.7896 ns | 0.7244 ns |  1.41 |    0.15 |    3 | 0.0019 |      32 B |        1.00 |
| Provider_MixedShorthand             | 19.381 ns | 1.2731 ns | 0.3306 ns |  1.71 |    0.17 |    3 | 0.0019 |      32 B |        1.00 |
