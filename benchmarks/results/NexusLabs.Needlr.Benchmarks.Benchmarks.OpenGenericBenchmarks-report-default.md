
BenchmarkDotNet v0.14.0, Ubuntu 24.04.4 LTS (Noble Numbat)
12th Gen Intel Core i7-12700H, 1 CPU, 20 logical and 10 physical cores
.NET SDK 10.0.302
  [Host]   : .NET 10.0.10 (10.0.1026.32716), X64 RyuJIT AVX2
  ShortRun : .NET 10.0.10 (10.0.1026.32716), X64 RyuJIT AVX2

Job=ShortRun  IterationCount=5  LaunchCount=1  
WarmupCount=3  

 Method                               | Mean     | Error    | StdDev   | Ratio | RatioSD | Rank | Gen0   | Allocated | Alloc Ratio |
------------------------------------- |---------:|---------:|---------:|------:|--------:|-----:|-------:|----------:|------------:|
 ManualDI_ResolveOpenGeneric          | 16.80 ns | 0.993 ns | 0.258 ns |  1.00 |    0.02 |    1 | 0.0019 |      24 B |        1.00 |
 Needlr_Reflection_ResolveOpenGeneric | 18.41 ns | 4.951 ns | 1.286 ns |  1.10 |    0.07 |    1 | 0.0019 |      24 B |        1.00 |
 Needlr_SourceGen_ResolveOpenGeneric  | 15.20 ns | 2.460 ns | 0.381 ns |  0.90 |    0.02 |    1 | 0.0019 |      24 B |        1.00 |
