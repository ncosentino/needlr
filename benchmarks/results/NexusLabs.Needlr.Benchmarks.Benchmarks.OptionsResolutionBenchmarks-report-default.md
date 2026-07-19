
BenchmarkDotNet v0.14.0, Ubuntu 24.04.4 LTS (Noble Numbat)
AMD EPYC 9V74, 1 CPU, 4 logical and 2 physical cores
.NET SDK 10.0.302
  [Host]   : .NET 10.0.10 (10.0.1026.32716), X64 RyuJIT AVX2
  ShortRun : .NET 10.0.10 (10.0.1026.32716), X64 RyuJIT AVX2

Job=ShortRun  IterationCount=5  LaunchCount=1  
WarmupCount=3  

 Method                           | Mean     | Error    | StdDev   | Ratio | Rank | Allocated | Alloc Ratio |
--------------------------------- |---------:|---------:|---------:|------:|-----:|----------:|------------:|
 ManualDI_ResolveOptions          | 25.08 ns | 0.072 ns | 0.019 ns |  1.00 |    1 |         - |          NA |
 Needlr_Reflection_ResolveOptions | 22.94 ns | 0.104 ns | 0.016 ns |  0.91 |    1 |         - |          NA |
 Needlr_SourceGen_ResolveOptions  | 21.98 ns | 0.286 ns | 0.074 ns |  0.88 |    1 |         - |          NA |
