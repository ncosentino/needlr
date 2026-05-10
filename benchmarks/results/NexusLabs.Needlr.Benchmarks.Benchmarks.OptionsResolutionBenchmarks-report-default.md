
BenchmarkDotNet v0.14.0, Ubuntu 24.04.4 LTS (Noble Numbat)
AMD EPYC 9V74, 1 CPU, 4 logical and 2 physical cores
.NET SDK 10.0.203
  [Host]   : .NET 10.0.7 (10.0.726.21808), X64 RyuJIT AVX2
  ShortRun : .NET 10.0.7 (10.0.726.21808), X64 RyuJIT AVX2

Job=ShortRun  IterationCount=5  LaunchCount=1  
WarmupCount=3  

 Method                           | Mean     | Error    | StdDev   | Ratio | Rank | Allocated | Alloc Ratio |
--------------------------------- |---------:|---------:|---------:|------:|-----:|----------:|------------:|
 ManualDI_ResolveOptions          | 21.47 ns | 0.098 ns | 0.015 ns |  1.00 |    1 |         - |          NA |
 Needlr_Reflection_ResolveOptions | 21.44 ns | 0.433 ns | 0.112 ns |  1.00 |    1 |         - |          NA |
 Needlr_SourceGen_ResolveOptions  | 20.93 ns | 0.053 ns | 0.008 ns |  0.97 |    1 |         - |          NA |
