
BenchmarkDotNet v0.14.0, Ubuntu 24.04.4 LTS (Noble Numbat)
AMD EPYC 9V74, 1 CPU, 4 logical and 2 physical cores
.NET SDK 10.0.201
  [Host]   : .NET 10.0.5 (10.0.526.15411), X64 RyuJIT AVX2
  ShortRun : .NET 10.0.5 (10.0.526.15411), X64 RyuJIT AVX2

Job=ShortRun  IterationCount=5  LaunchCount=1  
WarmupCount=3  

 Method                             | Mean     | Error    | StdDev   | Ratio | RatioSD | Rank | Allocated | Alloc Ratio |
----------------------------------- |---------:|---------:|---------:|------:|--------:|-----:|----------:|------------:|
 ManualDI_ResolveDecorated          | 16.08 ns | 1.416 ns | 0.368 ns |  1.00 |    0.03 |    1 |         - |          NA |
 Needlr_Reflection_ResolveDecorated | 16.68 ns | 0.154 ns | 0.040 ns |  1.04 |    0.02 |    1 |         - |          NA |
 Needlr_SourceGen_ResolveDecorated  | 21.06 ns | 1.123 ns | 0.174 ns |  1.31 |    0.03 |    2 |         - |          NA |
