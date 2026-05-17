
BenchmarkDotNet v0.14.0, Ubuntu 24.04.4 LTS (Noble Numbat)
AMD EPYC 9V74, 1 CPU, 4 logical and 2 physical cores
.NET SDK 10.0.300
  [Host]   : .NET 10.0.8 (10.0.826.23019), X64 RyuJIT AVX2
  ShortRun : .NET 10.0.8 (10.0.826.23019), X64 RyuJIT AVX2

Job=ShortRun  IterationCount=5  LaunchCount=1  
WarmupCount=3  

 Method                              | Mean     | Error    | StdDev   | Ratio | Rank | Gen0   | Allocated | Alloc Ratio |
------------------------------------ |---------:|---------:|---------:|------:|-----:|-------:|----------:|------------:|
 ManualDI_ResolveCollection          | 51.90 ns | 1.424 ns | 0.220 ns |  1.00 |    1 | 0.0029 |      48 B |        1.00 |
 Needlr_Reflection_ResolveCollection | 48.71 ns | 0.838 ns | 0.218 ns |  0.94 |    1 | 0.0029 |      48 B |        1.00 |
 Needlr_SourceGen_ResolveCollection  | 49.39 ns | 0.487 ns | 0.075 ns |  0.95 |    1 | 0.0029 |      48 B |        1.00 |
