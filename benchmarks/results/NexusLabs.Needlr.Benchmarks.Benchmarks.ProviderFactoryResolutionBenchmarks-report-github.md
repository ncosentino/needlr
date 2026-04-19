```

BenchmarkDotNet v0.14.0, Ubuntu 24.04.4 LTS (Noble Numbat)
AMD EPYC 9V74, 1 CPU, 4 logical and 2 physical cores
.NET SDK 10.0.202
  [Host]   : .NET 10.0.6 (10.0.626.17701), X64 RyuJIT AVX2
  ShortRun : .NET 10.0.6 (10.0.626.17701), X64 RyuJIT AVX2

Job=ShortRun  IterationCount=5  LaunchCount=1  
WarmupCount=3  

```
| Method                              | Mean      | Error     | StdDev    | Ratio | RatioSD | Rank | Gen0   | Allocated | Alloc Ratio |
|------------------------------------ |----------:|----------:|----------:|------:|--------:|-----:|-------:|----------:|------------:|
| ManualDI_FuncFactory_PreResolved    |  8.761 ns | 0.2132 ns | 0.0554 ns |  1.00 |    0.01 |    1 | 0.0019 |      32 B |        1.00 |
| ManualDI_FuncFactory_WithResolution | 23.286 ns | 0.8794 ns | 0.2284 ns |  2.66 |    0.03 |    2 | 0.0019 |      32 B |        1.00 |
| DirectFactory_PreResolved           | 10.013 ns | 0.8505 ns | 0.2209 ns |  1.14 |    0.02 |    1 | 0.0019 |      32 B |        1.00 |
| DirectFactory_WithResolution        | 25.154 ns | 0.7163 ns | 0.1108 ns |  2.87 |    0.02 |    2 | 0.0019 |      32 B |        1.00 |
| Provider_FactoryShorthand           |  9.537 ns | 0.6982 ns | 0.1813 ns |  1.09 |    0.02 |    1 | 0.0019 |      32 B |        1.00 |
| Provider_FactoryInterface           |  8.887 ns | 0.7776 ns | 0.2019 ns |  1.01 |    0.02 |    1 | 0.0019 |      32 B |        1.00 |
| Provider_MixedShorthand             |  9.299 ns | 2.0705 ns | 0.3204 ns |  1.06 |    0.03 |    1 | 0.0019 |      32 B |        1.00 |
