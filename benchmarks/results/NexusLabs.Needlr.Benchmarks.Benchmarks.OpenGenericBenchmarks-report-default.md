
BenchmarkDotNet v0.14.0, Ubuntu 24.04.3 LTS (Noble Numbat)
AMD EPYC 7763, 1 CPU, 4 logical and 2 physical cores
.NET SDK 10.0.102
  [Host]   : .NET 10.0.2 (10.0.225.61305), X64 RyuJIT AVX2
  ShortRun : .NET 10.0.2 (10.0.225.61305), X64 RyuJIT AVX2

Job=ShortRun  IterationCount=5  LaunchCount=1  
WarmupCount=3  

 Method                               | Mean     | Error    | StdDev   | Ratio | RatioSD | Rank | Gen0   | Allocated | Alloc Ratio |
------------------------------------- |---------:|---------:|---------:|------:|--------:|-----:|-------:|----------:|------------:|
 ManualDI_ResolveOpenGeneric          | 20.57 ns | 0.312 ns | 0.081 ns |  1.00 |    0.01 |    1 | 0.0014 |      24 B |        1.00 |
 Needlr_Reflection_ResolveOpenGeneric | 19.10 ns | 1.732 ns | 0.450 ns |  0.93 |    0.02 |    1 | 0.0014 |      24 B |        1.00 |
 Needlr_SourceGen_ResolveOpenGeneric  | 18.80 ns | 0.246 ns | 0.038 ns |  0.91 |    0.00 |    1 | 0.0014 |      24 B |        1.00 |
