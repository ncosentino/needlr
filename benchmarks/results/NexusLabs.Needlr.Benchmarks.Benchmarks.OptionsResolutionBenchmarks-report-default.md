
BenchmarkDotNet v0.14.0, Ubuntu 24.04.5 LTS (Noble Numbat)
AMD EPYC 9V74, 1 CPU, 4 logical and 2 physical cores
.NET SDK 10.0.303
  [Host]   : .NET 10.0.11 (10.0.1126.37416), X64 RyuJIT AVX-512F+CD+BW+DQ+VL+VBMI
  ShortRun : .NET 10.0.11 (10.0.1126.37416), X64 RyuJIT AVX-512F+CD+BW+DQ+VL+VBMI

Job=ShortRun  IterationCount=5  LaunchCount=1  
WarmupCount=3  

 Method                           | Mean     | Error    | StdDev   | Ratio | Rank | Allocated | Alloc Ratio |
--------------------------------- |---------:|---------:|---------:|------:|-----:|----------:|------------:|
 ManualDI_ResolveOptions          | 19.05 ns | 0.062 ns | 0.016 ns |  1.00 |    1 |         - |          NA |
 Needlr_Reflection_ResolveOptions | 18.64 ns | 0.072 ns | 0.019 ns |  0.98 |    1 |         - |          NA |
 Needlr_SourceGen_ResolveOptions  | 18.79 ns | 0.053 ns | 0.014 ns |  0.99 |    1 |         - |          NA |
