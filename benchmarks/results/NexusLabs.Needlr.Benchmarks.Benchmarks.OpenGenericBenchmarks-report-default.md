
BenchmarkDotNet v0.14.0, Ubuntu 24.04.4 LTS (Noble Numbat)
AMD EPYC 7763, 1 CPU, 4 logical and 2 physical cores
.NET SDK 10.0.301
  [Host]   : .NET 10.0.9 (10.0.926.27113), X64 RyuJIT AVX2
  ShortRun : .NET 10.0.9 (10.0.926.27113), X64 RyuJIT AVX2

Job=ShortRun  IterationCount=5  LaunchCount=1  
WarmupCount=3  

 Method                               | Mean     | Error    | StdDev   | Ratio | Rank | Gen0   | Allocated | Alloc Ratio |
------------------------------------- |---------:|---------:|---------:|------:|-----:|-------:|----------:|------------:|
 ManualDI_ResolveOpenGeneric          | 20.96 ns | 1.095 ns | 0.169 ns |  1.00 |    1 | 0.0014 |      24 B |        1.00 |
 Needlr_Reflection_ResolveOpenGeneric | 20.35 ns | 0.366 ns | 0.057 ns |  0.97 |    1 | 0.0014 |      24 B |        1.00 |
 Needlr_SourceGen_ResolveOpenGeneric  | 20.40 ns | 0.307 ns | 0.080 ns |  0.97 |    1 | 0.0014 |      24 B |        1.00 |
