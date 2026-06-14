
BenchmarkDotNet v0.14.0, Ubuntu 24.04.4 LTS (Noble Numbat)
AMD EPYC 7763, 1 CPU, 4 logical and 2 physical cores
.NET SDK 10.0.301
  [Host]   : .NET 10.0.9 (10.0.926.27113), X64 RyuJIT AVX2
  ShortRun : .NET 10.0.9 (10.0.926.27113), X64 RyuJIT AVX2

Job=ShortRun  IterationCount=5  LaunchCount=1  
WarmupCount=3  

 Method                             | Mean     | Error    | StdDev   | Ratio | Rank | Allocated | Alloc Ratio |
----------------------------------- |---------:|---------:|---------:|------:|-----:|----------:|------------:|
 ManualDI_ResolveDecorated          | 15.65 ns | 0.087 ns | 0.013 ns |  1.00 |    1 |         - |          NA |
 Needlr_Reflection_ResolveDecorated | 16.12 ns | 0.336 ns | 0.087 ns |  1.03 |    1 |         - |          NA |
 Needlr_SourceGen_ResolveDecorated  | 15.41 ns | 0.816 ns | 0.212 ns |  0.99 |    1 |         - |          NA |
