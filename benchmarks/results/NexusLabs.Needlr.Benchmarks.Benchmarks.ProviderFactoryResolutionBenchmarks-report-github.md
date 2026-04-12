```

BenchmarkDotNet v0.14.0, Ubuntu 24.04.4 LTS (Noble Numbat)
AMD EPYC 9V74, 1 CPU, 4 logical and 2 physical cores
.NET SDK 10.0.201
  [Host]   : .NET 10.0.5 (10.0.526.15411), X64 RyuJIT AVX2
  ShortRun : .NET 10.0.5 (10.0.526.15411), X64 RyuJIT AVX2

Job=ShortRun  IterationCount=5  LaunchCount=1  
WarmupCount=3  

```
| Method                              | Mean      | Error     | StdDev    | Ratio | RatioSD | Rank | Gen0   | Allocated | Alloc Ratio |
|------------------------------------ |----------:|----------:|----------:|------:|--------:|-----:|-------:|----------:|------------:|
| ManualDI_FuncFactory_PreResolved    |  8.544 ns | 0.4843 ns | 0.1258 ns |  1.00 |    0.02 |    1 | 0.0019 |      32 B |        1.00 |
| ManualDI_FuncFactory_WithResolution | 22.833 ns | 0.3176 ns | 0.0825 ns |  2.67 |    0.04 |    2 | 0.0019 |      32 B |        1.00 |
| DirectFactory_PreResolved           |  8.197 ns | 0.4732 ns | 0.0732 ns |  0.96 |    0.02 |    1 | 0.0019 |      32 B |        1.00 |
| DirectFactory_WithResolution        | 25.882 ns | 0.7717 ns | 0.2004 ns |  3.03 |    0.05 |    2 | 0.0019 |      32 B |        1.00 |
| Provider_FactoryShorthand           |  8.208 ns | 0.7727 ns | 0.1196 ns |  0.96 |    0.02 |    1 | 0.0019 |      32 B |        1.00 |
| Provider_FactoryInterface           |  8.579 ns | 1.4118 ns | 0.3667 ns |  1.00 |    0.04 |    1 | 0.0019 |      32 B |        1.00 |
| Provider_MixedShorthand             |  9.439 ns | 1.3849 ns | 0.2143 ns |  1.10 |    0.03 |    1 | 0.0019 |      32 B |        1.00 |
