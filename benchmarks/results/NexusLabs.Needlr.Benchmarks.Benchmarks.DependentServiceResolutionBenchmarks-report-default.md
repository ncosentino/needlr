
BenchmarkDotNet v0.14.0, Ubuntu 24.04.4 LTS (Noble Numbat)
AMD EPYC 7763, 1 CPU, 4 logical and 2 physical cores
.NET SDK 10.0.302
  [Host]   : .NET 10.0.11 (10.0.1126.37416), X64 RyuJIT AVX2
  ShortRun : .NET 10.0.11 (10.0.1126.37416), X64 RyuJIT AVX2

Job=ShortRun  IterationCount=5  LaunchCount=1  
WarmupCount=3  

 Method                             | Mean     | Error    | StdDev   | Ratio | RatioSD | Rank | Allocated | Alloc Ratio |
----------------------------------- |---------:|---------:|---------:|------:|--------:|-----:|----------:|------------:|
 ManualDI_ResolveDependent          | 15.16 ns | 0.030 ns | 0.005 ns |  1.00 |    0.00 |    1 |         - |          NA |
 Needlr_Reflection_ResolveDependent | 15.23 ns | 0.085 ns | 0.013 ns |  1.00 |    0.00 |    1 |         - |          NA |
 Needlr_SourceGen_ResolveDependent  | 15.40 ns | 3.556 ns | 0.923 ns |  1.02 |    0.06 |    1 |         - |          NA |
