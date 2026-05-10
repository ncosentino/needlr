
BenchmarkDotNet v0.14.0, Ubuntu 24.04.4 LTS (Noble Numbat)
AMD EPYC 9V74, 1 CPU, 4 logical and 2 physical cores
.NET SDK 10.0.203
  [Host]   : .NET 10.0.7 (10.0.726.21808), X64 RyuJIT AVX2
  ShortRun : .NET 10.0.7 (10.0.726.21808), X64 RyuJIT AVX2

Job=ShortRun  IterationCount=5  LaunchCount=1  
WarmupCount=3  

 Method                               | Mean     | Error    | StdDev   | Ratio | RatioSD | Rank | Gen0   | Allocated | Alloc Ratio |
------------------------------------- |---------:|---------:|---------:|------:|--------:|-----:|-------:|----------:|------------:|
 ManualDI_ResolveOpenGeneric          | 19.70 ns | 1.680 ns | 0.260 ns |  1.00 |    0.02 |    1 | 0.0014 |      24 B |        1.00 |
 Needlr_Reflection_ResolveOpenGeneric | 23.55 ns | 0.511 ns | 0.079 ns |  1.20 |    0.01 |    1 | 0.0014 |      24 B |        1.00 |
 Needlr_SourceGen_ResolveOpenGeneric  | 20.24 ns | 0.584 ns | 0.090 ns |  1.03 |    0.01 |    1 | 0.0014 |      24 B |        1.00 |
