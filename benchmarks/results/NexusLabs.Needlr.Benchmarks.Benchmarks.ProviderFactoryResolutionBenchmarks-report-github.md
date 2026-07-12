```

BenchmarkDotNet v0.14.0, Ubuntu 24.04.4 LTS (Noble Numbat)
AMD EPYC 7763, 1 CPU, 4 logical and 2 physical cores
.NET SDK 10.0.301
  [Host]   : .NET 10.0.9 (10.0.926.27113), X64 RyuJIT AVX2
  ShortRun : .NET 10.0.9 (10.0.926.27113), X64 RyuJIT AVX2

Job=ShortRun  IterationCount=5  LaunchCount=1  
WarmupCount=3  

```
| Method                              | Mean     | Error    | StdDev   | Ratio | RatioSD | Rank | Gen0   | Allocated | Alloc Ratio |
|------------------------------------ |---------:|---------:|---------:|------:|--------:|-----:|-------:|----------:|------------:|
| ManualDI_FuncFactory_PreResolved    | 14.85 ns | 8.048 ns | 1.245 ns |  1.01 |    0.11 |    2 | 0.0019 |      32 B |        1.00 |
| ManualDI_FuncFactory_WithResolution | 19.72 ns | 0.451 ns | 0.117 ns |  1.34 |    0.11 |    2 | 0.0019 |      32 B |        1.00 |
| DirectFactory_PreResolved           | 10.52 ns | 2.422 ns | 0.629 ns |  0.71 |    0.07 |    1 | 0.0019 |      32 B |        1.00 |
| DirectFactory_WithResolution        | 20.15 ns | 0.199 ns | 0.052 ns |  1.36 |    0.11 |    2 | 0.0019 |      32 B |        1.00 |
| Provider_FactoryShorthand           | 10.94 ns | 0.377 ns | 0.058 ns |  0.74 |    0.06 |    1 | 0.0019 |      32 B |        1.00 |
| Provider_FactoryInterface           | 17.01 ns | 2.792 ns | 0.725 ns |  1.15 |    0.10 |    2 | 0.0019 |      32 B |        1.00 |
| Provider_MixedShorthand             | 16.25 ns | 3.366 ns | 0.521 ns |  1.10 |    0.09 |    2 | 0.0019 |      32 B |        1.00 |
