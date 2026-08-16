```

BenchmarkDotNet v0.14.0, Ubuntu 24.04.4 LTS (Noble Numbat)
Intel Core 7 150U, 1 CPU, 12 logical and 6 physical cores
.NET SDK 10.0.302
  [Host]   : .NET 10.0.10 (10.0.1026.32716), X64 RyuJIT AVX2
  ShortRun : .NET 10.0.10 (10.0.1026.32716), X64 RyuJIT AVX2

Job=ShortRun  IterationCount=5  LaunchCount=1  
WarmupCount=3  

```
| Method                              | Mean      | Error    | StdDev    | Ratio | RatioSD | Rank | Gen0   | Allocated | Alloc Ratio |
|------------------------------------ |----------:|---------:|----------:|------:|--------:|-----:|-------:|----------:|------------:|
| ManualDI_FuncFactory_PreResolved    |  6.693 ns | 3.095 ns | 0.8039 ns |  1.01 |    0.15 |    1 | 0.0051 |      32 B |        1.00 |
| ManualDI_FuncFactory_WithResolution | 15.613 ns | 3.433 ns | 0.5312 ns |  2.36 |    0.25 |    2 | 0.0051 |      32 B |        1.00 |
| DirectFactory_PreResolved           |  6.856 ns | 3.678 ns | 0.9552 ns |  1.04 |    0.17 |    1 | 0.0051 |      32 B |        1.00 |
| DirectFactory_WithResolution        | 16.945 ns | 5.064 ns | 0.7836 ns |  2.56 |    0.28 |    2 | 0.0051 |      32 B |        1.00 |
| Provider_FactoryShorthand           |  7.810 ns | 4.260 ns | 1.1064 ns |  1.18 |    0.20 |    1 | 0.0051 |      32 B |        1.00 |
| Provider_FactoryInterface           |  6.397 ns | 3.459 ns | 0.8984 ns |  0.97 |    0.16 |    1 | 0.0051 |      32 B |        1.00 |
| Provider_MixedShorthand             |  7.805 ns | 5.159 ns | 0.7984 ns |  1.18 |    0.16 |    1 | 0.0051 |      32 B |        1.00 |
