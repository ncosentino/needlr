
BenchmarkDotNet v0.14.0, Ubuntu 24.04.5 LTS (Noble Numbat)
AMD EPYC 9V74, 1 CPU, 4 logical and 2 physical cores
.NET SDK 10.0.303
  [Host]   : .NET 10.0.11 (10.0.1126.37416), X64 RyuJIT AVX-512F+CD+BW+DQ+VL+VBMI
  ShortRun : .NET 10.0.11 (10.0.1126.37416), X64 RyuJIT AVX-512F+CD+BW+DQ+VL+VBMI

Job=ShortRun  IterationCount=5  LaunchCount=1  
WarmupCount=3  

 Method                              | Mean      | Error     | StdDev    | Ratio | RatioSD | Rank | Gen0   | Allocated | Alloc Ratio |
------------------------------------ |----------:|----------:|----------:|------:|--------:|-----:|-------:|----------:|------------:|
 ManualDI_FuncFactory_PreResolved    |  7.173 ns | 0.5180 ns | 0.1345 ns |  1.00 |    0.02 |    1 | 0.0019 |      32 B |        1.00 |
 ManualDI_FuncFactory_WithResolution | 18.697 ns | 0.4721 ns | 0.1226 ns |  2.61 |    0.05 |    2 | 0.0019 |      32 B |        1.00 |
 DirectFactory_PreResolved           |  6.626 ns | 0.7526 ns | 0.1165 ns |  0.92 |    0.02 |    1 | 0.0019 |      32 B |        1.00 |
 DirectFactory_WithResolution        | 22.393 ns | 3.6628 ns | 0.9512 ns |  3.12 |    0.13 |    2 | 0.0019 |      32 B |        1.00 |
 Provider_FactoryShorthand           |  6.920 ns | 0.4802 ns | 0.1247 ns |  0.96 |    0.02 |    1 | 0.0019 |      32 B |        1.00 |
 Provider_FactoryInterface           |  7.899 ns | 0.4965 ns | 0.1289 ns |  1.10 |    0.02 |    1 | 0.0019 |      32 B |        1.00 |
 Provider_MixedShorthand             |  6.989 ns | 0.2264 ns | 0.0350 ns |  0.97 |    0.02 |    1 | 0.0019 |      32 B |        1.00 |
