
BenchmarkDotNet v0.14.0, Ubuntu 24.04.5 LTS (Noble Numbat)
AMD EPYC 9V74, 1 CPU, 4 logical and 2 physical cores
.NET SDK 10.0.303
  [Host]   : .NET 10.0.11 (10.0.1126.37416), X64 RyuJIT AVX-512F+CD+BW+DQ+VL+VBMI
  ShortRun : .NET 10.0.11 (10.0.1126.37416), X64 RyuJIT AVX-512F+CD+BW+DQ+VL+VBMI

Job=ShortRun  IterationCount=5  LaunchCount=1  
WarmupCount=3  

 Method                             | Mean     | Error    | StdDev   | Ratio | RatioSD | Rank | Allocated | Alloc Ratio |
----------------------------------- |---------:|---------:|---------:|------:|--------:|-----:|----------:|------------:|
 ManualDI_ResolveDecorated          | 12.13 ns | 0.782 ns | 0.203 ns |  1.00 |    0.02 |    1 |         - |          NA |
 Needlr_Reflection_ResolveDecorated | 14.15 ns | 0.108 ns | 0.017 ns |  1.17 |    0.02 |    1 |         - |          NA |
 Needlr_SourceGen_ResolveDecorated  | 14.96 ns | 0.500 ns | 0.130 ns |  1.23 |    0.02 |    1 |         - |          NA |
