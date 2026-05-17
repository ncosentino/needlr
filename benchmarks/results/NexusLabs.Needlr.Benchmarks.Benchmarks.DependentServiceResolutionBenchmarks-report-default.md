
BenchmarkDotNet v0.14.0, Ubuntu 24.04.4 LTS (Noble Numbat)
AMD EPYC 9V74, 1 CPU, 4 logical and 2 physical cores
.NET SDK 10.0.300
  [Host]   : .NET 10.0.8 (10.0.826.23019), X64 RyuJIT AVX2
  ShortRun : .NET 10.0.8 (10.0.826.23019), X64 RyuJIT AVX2

Job=ShortRun  IterationCount=5  LaunchCount=1  
WarmupCount=3  

 Method                             | Mean     | Error    | StdDev   | Ratio | Rank | Allocated | Alloc Ratio |
----------------------------------- |---------:|---------:|---------:|------:|-----:|----------:|------------:|
 ManualDI_ResolveDependent          | 16.19 ns | 0.414 ns | 0.108 ns |  1.00 |    1 |         - |          NA |
 Needlr_Reflection_ResolveDependent | 15.91 ns | 0.063 ns | 0.016 ns |  0.98 |    1 |         - |          NA |
 Needlr_SourceGen_ResolveDependent  | 18.95 ns | 0.350 ns | 0.091 ns |  1.17 |    1 |         - |          NA |
