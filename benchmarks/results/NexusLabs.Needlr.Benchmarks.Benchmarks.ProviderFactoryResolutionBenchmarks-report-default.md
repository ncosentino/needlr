
BenchmarkDotNet v0.14.0, Ubuntu 24.04.4 LTS (Noble Numbat)
AMD EPYC 9V74, 1 CPU, 4 logical and 2 physical cores
.NET SDK 10.0.300
  [Host]   : .NET 10.0.8 (10.0.826.23019), X64 RyuJIT AVX2
  ShortRun : .NET 10.0.8 (10.0.826.23019), X64 RyuJIT AVX2

Job=ShortRun  IterationCount=5  LaunchCount=1  
WarmupCount=3  

 Method                              | Mean      | Error     | StdDev    | Ratio | RatioSD | Rank | Gen0   | Allocated | Alloc Ratio |
------------------------------------ |----------:|----------:|----------:|------:|--------:|-----:|-------:|----------:|------------:|
 ManualDI_FuncFactory_PreResolved    |  8.889 ns | 0.8501 ns | 0.2208 ns |  1.00 |    0.03 |    1 | 0.0019 |      32 B |        1.00 |
 ManualDI_FuncFactory_WithResolution | 24.152 ns | 0.2666 ns | 0.0692 ns |  2.72 |    0.06 |    2 | 0.0019 |      32 B |        1.00 |
 DirectFactory_PreResolved           |  8.609 ns | 0.4298 ns | 0.1116 ns |  0.97 |    0.03 |    1 | 0.0019 |      32 B |        1.00 |
 DirectFactory_WithResolution        | 25.097 ns | 0.2076 ns | 0.0539 ns |  2.82 |    0.07 |    2 | 0.0019 |      32 B |        1.00 |
 Provider_FactoryShorthand           |  9.044 ns | 1.0624 ns | 0.2759 ns |  1.02 |    0.04 |    1 | 0.0019 |      32 B |        1.00 |
 Provider_FactoryInterface           |  8.764 ns | 0.7322 ns | 0.1902 ns |  0.99 |    0.03 |    1 | 0.0019 |      32 B |        1.00 |
 Provider_MixedShorthand             |  8.757 ns | 1.1362 ns | 0.1758 ns |  0.99 |    0.03 |    1 | 0.0019 |      32 B |        1.00 |
