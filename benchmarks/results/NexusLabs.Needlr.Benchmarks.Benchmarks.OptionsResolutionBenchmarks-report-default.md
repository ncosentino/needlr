
BenchmarkDotNet v0.14.0, Ubuntu 24.04.4 LTS (Noble Numbat)
AMD EPYC 7763, 1 CPU, 4 logical and 2 physical cores
.NET SDK 10.0.302
  [Host]   : .NET 10.0.11 (10.0.1126.37416), X64 RyuJIT AVX2
  ShortRun : .NET 10.0.11 (10.0.1126.37416), X64 RyuJIT AVX2

Job=ShortRun  IterationCount=5  LaunchCount=1  
WarmupCount=3  

 Method                           | Mean     | Error    | StdDev   | Ratio | RatioSD | Rank | Allocated | Alloc Ratio |
--------------------------------- |---------:|---------:|---------:|------:|--------:|-----:|----------:|------------:|
 ManualDI_ResolveOptions          | 17.24 ns | 1.721 ns | 0.447 ns |  1.00 |    0.03 |    1 |         - |          NA |
 Needlr_Reflection_ResolveOptions | 18.39 ns | 2.481 ns | 0.644 ns |  1.07 |    0.04 |    1 |         - |          NA |
 Needlr_SourceGen_ResolveOptions  | 17.26 ns | 1.177 ns | 0.182 ns |  1.00 |    0.03 |    1 |         - |          NA |
