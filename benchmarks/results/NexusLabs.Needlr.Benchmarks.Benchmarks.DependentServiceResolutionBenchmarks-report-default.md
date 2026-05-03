
BenchmarkDotNet v0.14.0, Ubuntu 24.04.4 LTS (Noble Numbat)
AMD EPYC 7763, 1 CPU, 4 logical and 2 physical cores
.NET SDK 10.0.203
  [Host]   : .NET 10.0.7 (10.0.726.21808), X64 RyuJIT AVX2
  ShortRun : .NET 10.0.7 (10.0.726.21808), X64 RyuJIT AVX2

Job=ShortRun  IterationCount=5  LaunchCount=1  
WarmupCount=3  

 Method                             | Mean     | Error    | StdDev   | Ratio | RatioSD | Rank | Allocated | Alloc Ratio |
----------------------------------- |---------:|---------:|---------:|------:|--------:|-----:|----------:|------------:|
 ManualDI_ResolveDependent          | 15.97 ns | 0.103 ns | 0.027 ns |  1.00 |    0.00 |    1 |         - |          NA |
 Needlr_Reflection_ResolveDependent | 16.49 ns | 4.569 ns | 1.186 ns |  1.03 |    0.07 |    1 |         - |          NA |
 Needlr_SourceGen_ResolveDependent  | 15.23 ns | 0.050 ns | 0.013 ns |  0.95 |    0.00 |    1 |         - |          NA |
