```

BenchmarkDotNet v0.14.0, Ubuntu 24.04.4 LTS (Noble Numbat)
AMD EPYC 9V74, 1 CPU, 4 logical and 2 physical cores
.NET SDK 10.0.203
  [Host]   : .NET 10.0.7 (10.0.726.21808), X64 RyuJIT AVX2
  ShortRun : .NET 10.0.7 (10.0.726.21808), X64 RyuJIT AVX2

Job=ShortRun  IterationCount=5  LaunchCount=1  
WarmupCount=3  

```
| Method                              | Mean      | Error     | StdDev    | Ratio | RatioSD | Rank | Gen0   | Allocated | Alloc Ratio |
|------------------------------------ |----------:|----------:|----------:|------:|--------:|-----:|-------:|----------:|------------:|
| ManualDI_FuncFactory_PreResolved    |  9.548 ns | 1.5166 ns | 0.3939 ns |  1.00 |    0.05 |    1 | 0.0019 |      32 B |        1.00 |
| ManualDI_FuncFactory_WithResolution | 23.732 ns | 0.3732 ns | 0.0969 ns |  2.49 |    0.10 |    2 | 0.0019 |      32 B |        1.00 |
| DirectFactory_PreResolved           |  8.759 ns | 1.1388 ns | 0.1762 ns |  0.92 |    0.04 |    1 | 0.0019 |      32 B |        1.00 |
| DirectFactory_WithResolution        | 26.030 ns | 1.9547 ns | 0.5076 ns |  2.73 |    0.12 |    2 | 0.0019 |      32 B |        1.00 |
| Provider_FactoryShorthand           |  9.141 ns | 0.7398 ns | 0.1921 ns |  0.96 |    0.04 |    1 | 0.0019 |      32 B |        1.00 |
| Provider_FactoryInterface           |  8.727 ns | 0.4621 ns | 0.1200 ns |  0.92 |    0.04 |    1 | 0.0019 |      32 B |        1.00 |
| Provider_MixedShorthand             |  9.307 ns | 0.3276 ns | 0.0851 ns |  0.98 |    0.04 |    1 | 0.0019 |      32 B |        1.00 |
