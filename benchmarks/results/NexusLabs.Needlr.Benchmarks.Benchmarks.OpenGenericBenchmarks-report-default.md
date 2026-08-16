
BenchmarkDotNet v0.14.0, Ubuntu 24.04.4 LTS (Noble Numbat)
Intel Core 7 150U, 1 CPU, 12 logical and 6 physical cores
.NET SDK 10.0.302
  [Host]   : .NET 10.0.10 (10.0.1026.32716), X64 RyuJIT AVX2
  ShortRun : .NET 10.0.10 (10.0.1026.32716), X64 RyuJIT AVX2

Job=ShortRun  IterationCount=5  LaunchCount=1  
WarmupCount=3  

 Method                               | Mean     | Error    | StdDev   | Ratio | RatioSD | Rank | Gen0   | Allocated | Alloc Ratio |
------------------------------------- |---------:|---------:|---------:|------:|--------:|-----:|-------:|----------:|------------:|
 ManualDI_ResolveOpenGeneric          | 15.75 ns | 6.430 ns | 1.670 ns |  1.01 |    0.13 |    1 | 0.0038 |      24 B |        1.00 |
 Needlr_Reflection_ResolveOpenGeneric | 14.46 ns | 5.911 ns | 0.915 ns |  0.93 |    0.10 |    1 | 0.0038 |      24 B |        1.00 |
 Needlr_SourceGen_ResolveOpenGeneric  | 14.59 ns | 7.149 ns | 1.106 ns |  0.93 |    0.11 |    1 | 0.0038 |      24 B |        1.00 |
