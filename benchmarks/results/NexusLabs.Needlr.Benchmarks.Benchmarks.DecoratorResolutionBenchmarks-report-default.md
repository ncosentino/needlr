
BenchmarkDotNet v0.14.0, Ubuntu 24.04.4 LTS (Noble Numbat)
AMD EPYC 7763, 1 CPU, 4 logical and 2 physical cores
.NET SDK 10.0.301
  [Host]   : .NET 10.0.9 (10.0.926.27113), X64 RyuJIT AVX2
  ShortRun : .NET 10.0.9 (10.0.926.27113), X64 RyuJIT AVX2

Job=ShortRun  IterationCount=5  LaunchCount=1  
WarmupCount=3  

 Method                             | Mean     | Error    | StdDev   | Ratio | RatioSD | Rank | Allocated | Alloc Ratio |
----------------------------------- |---------:|---------:|---------:|------:|--------:|-----:|----------:|------------:|
 ManualDI_ResolveDecorated          | 15.19 ns | 0.821 ns | 0.213 ns |  1.00 |    0.02 |    1 |         - |          NA |
 Needlr_Reflection_ResolveDecorated | 15.89 ns | 0.248 ns | 0.038 ns |  1.05 |    0.01 |    1 |         - |          NA |
 Needlr_SourceGen_ResolveDecorated  | 15.03 ns | 0.053 ns | 0.014 ns |  0.99 |    0.01 |    1 |         - |          NA |
