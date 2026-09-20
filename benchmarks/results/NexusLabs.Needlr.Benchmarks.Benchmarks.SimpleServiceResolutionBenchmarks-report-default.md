
BenchmarkDotNet v0.14.0, Ubuntu 24.04.5 LTS (Noble Numbat)
AMD EPYC 9V74, 1 CPU, 4 logical and 2 physical cores
.NET SDK 10.0.303
  [Host]   : .NET 10.0.11 (10.0.1126.37416), X64 RyuJIT AVX-512F+CD+BW+DQ+VL+VBMI
  ShortRun : .NET 10.0.11 (10.0.1126.37416), X64 RyuJIT AVX-512F+CD+BW+DQ+VL+VBMI

Job=ShortRun  IterationCount=5  LaunchCount=1  
WarmupCount=3  

 Method                          | Mean     | Error    | StdDev   | Ratio | Rank | Allocated | Alloc Ratio |
-------------------------------- |---------:|---------:|---------:|------:|-----:|----------:|------------:|
 ManualDI_ResolveSimple          | 12.59 ns | 0.110 ns | 0.029 ns |  1.00 |    1 |         - |          NA |
 Needlr_Reflection_ResolveSimple | 15.20 ns | 0.067 ns | 0.010 ns |  1.21 |    1 |         - |          NA |
 Needlr_SourceGen_ResolveSimple  | 14.59 ns | 0.135 ns | 0.021 ns |  1.16 |    1 |         - |          NA |
