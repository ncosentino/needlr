
BenchmarkDotNet v0.14.0, Ubuntu 24.04.4 LTS (Noble Numbat)
AMD EPYC 9V74, 1 CPU, 4 logical and 2 physical cores
.NET SDK 10.0.201
  [Host]   : .NET 10.0.5 (10.0.526.15411), X64 RyuJIT AVX2
  ShortRun : .NET 10.0.5 (10.0.526.15411), X64 RyuJIT AVX2

Job=ShortRun  IterationCount=5  LaunchCount=1  
WarmupCount=3  

 Method                           | Mean     | Error    | StdDev   | Ratio | Rank | Allocated | Alloc Ratio |
--------------------------------- |---------:|---------:|---------:|------:|-----:|----------:|------------:|
 ManualDI_ResolveOptions          | 20.88 ns | 0.064 ns | 0.017 ns |  1.00 |    1 |         - |          NA |
 Needlr_Reflection_ResolveOptions | 21.42 ns | 0.288 ns | 0.075 ns |  1.03 |    1 |         - |          NA |
 Needlr_SourceGen_ResolveOptions  | 22.62 ns | 0.055 ns | 0.008 ns |  1.08 |    1 |         - |          NA |
