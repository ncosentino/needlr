
BenchmarkDotNet v0.14.0, Ubuntu 24.04.5 LTS (Noble Numbat)
AMD EPYC 9V74, 1 CPU, 4 logical and 2 physical cores
.NET SDK 10.0.404
  [Host]   : .NET 10.0.15 (10.0.1526.53008), X64 RyuJIT AVX-512F+CD+BW+DQ+VL+VBMI
  ShortRun : .NET 10.0.15 (10.0.1526.53008), X64 RyuJIT AVX-512F+CD+BW+DQ+VL+VBMI

Job=ShortRun  IterationCount=5  LaunchCount=1  
WarmupCount=3  

 Method                             | Mean     | Error    | StdDev   | Ratio | RatioSD | Rank | Allocated | Alloc Ratio |
----------------------------------- |---------:|---------:|---------:|------:|--------:|-----:|----------:|------------:|
 ManualDI_ResolveDependent          | 14.07 ns | 1.051 ns | 0.273 ns |  1.00 |    0.03 |    1 |         - |          NA |
 Needlr_Reflection_ResolveDependent | 11.87 ns | 0.149 ns | 0.039 ns |  0.84 |    0.02 |    1 |         - |          NA |
 Needlr_SourceGen_ResolveDependent  | 14.60 ns | 0.148 ns | 0.023 ns |  1.04 |    0.02 |    1 |         - |          NA |
