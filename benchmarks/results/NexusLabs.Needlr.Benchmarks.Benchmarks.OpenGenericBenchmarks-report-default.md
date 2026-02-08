
BenchmarkDotNet v0.14.0, Ubuntu 24.04.3 LTS (Noble Numbat)
AMD EPYC 7763, 1 CPU, 4 logical and 2 physical cores
.NET SDK 10.0.102
  [Host]   : .NET 10.0.2 (10.0.225.61305), X64 RyuJIT AVX2
  ShortRun : .NET 10.0.2 (10.0.225.61305), X64 RyuJIT AVX2

Job=ShortRun  IterationCount=5  LaunchCount=1  
WarmupCount=3  

 Method                               | Mean     | Error    | StdDev   | Ratio | Rank | Gen0   | Allocated | Alloc Ratio |
------------------------------------- |---------:|---------:|---------:|------:|-----:|-------:|----------:|------------:|
 ManualDI_ResolveOpenGeneric          | 19.87 ns | 0.284 ns | 0.044 ns |  1.00 |    1 | 0.0014 |      24 B |        1.00 |
 Needlr_Reflection_ResolveOpenGeneric | 19.07 ns | 0.489 ns | 0.127 ns |  0.96 |    1 | 0.0014 |      24 B |        1.00 |
 Needlr_SourceGen_ResolveOpenGeneric  | 19.20 ns | 0.661 ns | 0.172 ns |  0.97 |    1 | 0.0014 |      24 B |        1.00 |
