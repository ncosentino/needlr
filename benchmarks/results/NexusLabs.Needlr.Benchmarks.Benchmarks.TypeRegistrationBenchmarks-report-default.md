
BenchmarkDotNet v0.14.0, Ubuntu 24.04.4 LTS (Noble Numbat)
AMD EPYC 9V74, 1 CPU, 4 logical and 2 physical cores
.NET SDK 10.0.300
  [Host]   : .NET 10.0.8 (10.0.826.23019), X64 RyuJIT AVX2
  ShortRun : .NET 10.0.8 (10.0.826.23019), X64 RyuJIT AVX2

Job=ShortRun  IterationCount=5  LaunchCount=1  
WarmupCount=3  

 Method                          | Mean           | Error           | StdDev        | Ratio     | RatioSD  | Rank | Gen0    | Gen1   | Allocated | Alloc Ratio |
-------------------------------- |---------------:|----------------:|--------------:|----------:|---------:|-----:|--------:|-------:|----------:|------------:|
 ManualDI_RegisterTypes          |       107.5 ns |         4.02 ns |       1.04 ns |      1.00 |     0.01 |    1 |  0.0315 |      - |     528 B |        1.00 |
 Needlr_Reflection_RegisterTypes | 1,634,353.2 ns | 1,955,192.98 ns | 507,757.41 ns | 15,202.04 | 4,313.71 |    3 | 31.2500 |      - |  589829 B |    1,117.10 |
 Needlr_SourceGen_RegisterTypes  |    15,308.1 ns |       468.28 ns |      72.47 ns |    142.39 |     1.40 |    2 |  1.8005 | 0.1221 |   30440 B |       57.65 |
