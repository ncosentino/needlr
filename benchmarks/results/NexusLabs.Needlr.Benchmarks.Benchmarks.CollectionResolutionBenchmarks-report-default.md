
BenchmarkDotNet v0.14.0, Ubuntu 24.04.4 LTS (Noble Numbat)
AMD EPYC 7763, 1 CPU, 4 logical and 2 physical cores
.NET SDK 10.0.203
  [Host]   : .NET 10.0.7 (10.0.726.21808), X64 RyuJIT AVX2
  ShortRun : .NET 10.0.7 (10.0.726.21808), X64 RyuJIT AVX2

Job=ShortRun  IterationCount=5  LaunchCount=1  
WarmupCount=3  

 Method                              | Mean     | Error    | StdDev   | Ratio | Rank | Gen0   | Allocated | Alloc Ratio |
------------------------------------ |---------:|---------:|---------:|------:|-----:|-------:|----------:|------------:|
 ManualDI_ResolveCollection          | 42.59 ns | 0.370 ns | 0.057 ns |  1.00 |    1 | 0.0029 |      48 B |        1.00 |
 Needlr_Reflection_ResolveCollection | 43.71 ns | 1.456 ns | 0.225 ns |  1.03 |    1 | 0.0029 |      48 B |        1.00 |
 Needlr_SourceGen_ResolveCollection  | 46.82 ns | 1.301 ns | 0.338 ns |  1.10 |    1 | 0.0029 |      48 B |        1.00 |
