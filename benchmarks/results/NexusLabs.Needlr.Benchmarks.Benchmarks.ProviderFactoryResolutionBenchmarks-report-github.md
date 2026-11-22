```

BenchmarkDotNet v0.14.0, Ubuntu 24.04.5 LTS (Noble Numbat)
AMD EPYC 9V74, 1 CPU, 4 logical and 2 physical cores
.NET SDK 10.0.404
  [Host]   : .NET 10.0.15 (10.0.1526.53008), X64 RyuJIT AVX-512F+CD+BW+DQ+VL+VBMI
  ShortRun : .NET 10.0.15 (10.0.1526.53008), X64 RyuJIT AVX-512F+CD+BW+DQ+VL+VBMI

Job=ShortRun  IterationCount=5  LaunchCount=1  
WarmupCount=3  

```
| Method                              | Mean      | Error     | StdDev    | Ratio | RatioSD | Rank | Gen0   | Allocated | Alloc Ratio |
|------------------------------------ |----------:|----------:|----------:|------:|--------:|-----:|-------:|----------:|------------:|
| ManualDI_FuncFactory_PreResolved    |  6.492 ns | 0.0221 ns | 0.0034 ns |  1.00 |    0.00 |    1 | 0.0019 |      32 B |        1.00 |
| ManualDI_FuncFactory_WithResolution | 17.682 ns | 0.7063 ns | 0.1834 ns |  2.72 |    0.03 |    2 | 0.0019 |      32 B |        1.00 |
| DirectFactory_PreResolved           |  6.280 ns | 0.4989 ns | 0.1296 ns |  0.97 |    0.02 |    1 | 0.0019 |      32 B |        1.00 |
| DirectFactory_WithResolution        | 22.891 ns | 0.7852 ns | 0.2039 ns |  3.53 |    0.03 |    3 | 0.0019 |      32 B |        1.00 |
| Provider_FactoryShorthand           |  6.929 ns | 0.5421 ns | 0.1408 ns |  1.07 |    0.02 |    1 | 0.0019 |      32 B |        1.00 |
| Provider_FactoryInterface           |  6.906 ns | 0.4916 ns | 0.1277 ns |  1.06 |    0.02 |    1 | 0.0019 |      32 B |        1.00 |
| Provider_MixedShorthand             |  6.927 ns | 0.2290 ns | 0.0595 ns |  1.07 |    0.01 |    1 | 0.0019 |      32 B |        1.00 |
