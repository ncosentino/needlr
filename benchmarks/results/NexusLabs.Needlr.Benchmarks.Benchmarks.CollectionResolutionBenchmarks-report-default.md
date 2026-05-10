
BenchmarkDotNet v0.14.0, Ubuntu 24.04.4 LTS (Noble Numbat)
AMD EPYC 9V74, 1 CPU, 4 logical and 2 physical cores
.NET SDK 10.0.203
  [Host]   : .NET 10.0.7 (10.0.726.21808), X64 RyuJIT AVX2
  ShortRun : .NET 10.0.7 (10.0.726.21808), X64 RyuJIT AVX2

Job=ShortRun  IterationCount=5  LaunchCount=1  
WarmupCount=3  

 Method                              | Mean     | Error    | StdDev   | Ratio | Rank | Gen0   | Allocated | Alloc Ratio |
------------------------------------ |---------:|---------:|---------:|------:|-----:|-------:|----------:|------------:|
 ManualDI_ResolveCollection          | 48.52 ns | 0.176 ns | 0.027 ns |  1.00 |    1 | 0.0029 |      48 B |        1.00 |
 Needlr_Reflection_ResolveCollection | 47.59 ns | 0.412 ns | 0.064 ns |  0.98 |    1 | 0.0029 |      48 B |        1.00 |
 Needlr_SourceGen_ResolveCollection  | 47.72 ns | 0.152 ns | 0.024 ns |  0.98 |    1 | 0.0029 |      48 B |        1.00 |
