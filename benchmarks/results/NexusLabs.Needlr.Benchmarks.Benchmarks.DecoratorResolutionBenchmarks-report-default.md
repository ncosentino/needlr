
BenchmarkDotNet v0.14.0, Ubuntu 24.04.5 LTS (Noble Numbat)
AMD EPYC 9V74, 1 CPU, 4 logical and 2 physical cores
.NET SDK 10.0.404
  [Host]   : .NET 10.0.15 (10.0.1526.53008), X64 RyuJIT AVX-512F+CD+BW+DQ+VL+VBMI
  ShortRun : .NET 10.0.15 (10.0.1526.53008), X64 RyuJIT AVX-512F+CD+BW+DQ+VL+VBMI

Job=ShortRun  IterationCount=5  LaunchCount=1  
WarmupCount=3  

 Method                             | Mean     | Error    | StdDev   | Ratio | Rank | Allocated | Alloc Ratio |
----------------------------------- |---------:|---------:|---------:|------:|-----:|----------:|------------:|
 ManualDI_ResolveDecorated          | 12.68 ns | 0.150 ns | 0.023 ns |  1.00 |    1 |         - |          NA |
 Needlr_Reflection_ResolveDecorated | 14.87 ns | 0.462 ns | 0.071 ns |  1.17 |    1 |         - |          NA |
 Needlr_SourceGen_ResolveDecorated  | 14.15 ns | 0.159 ns | 0.041 ns |  1.12 |    1 |         - |          NA |
