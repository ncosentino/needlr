
BenchmarkDotNet v0.14.0, Ubuntu 24.04.4 LTS (Noble Numbat)
AMD EPYC 7763, 1 CPU, 4 logical and 2 physical cores
.NET SDK 10.0.301
  [Host]   : .NET 10.0.9 (10.0.926.27113), X64 RyuJIT AVX2
  ShortRun : .NET 10.0.9 (10.0.926.27113), X64 RyuJIT AVX2

Job=ShortRun  IterationCount=5  LaunchCount=1  
WarmupCount=3  

 Method                              | Mean     | Error    | StdDev   | Ratio | Rank | Gen0   | Allocated | Alloc Ratio |
------------------------------------ |---------:|---------:|---------:|------:|-----:|-------:|----------:|------------:|
 ManualDI_ResolveCollection          | 45.22 ns | 0.649 ns | 0.100 ns |  1.00 |    1 | 0.0029 |      48 B |        1.00 |
 Needlr_Reflection_ResolveCollection | 42.49 ns | 0.896 ns | 0.233 ns |  0.94 |    1 | 0.0029 |      48 B |        1.00 |
 Needlr_SourceGen_ResolveCollection  | 43.24 ns | 0.463 ns | 0.072 ns |  0.96 |    1 | 0.0029 |      48 B |        1.00 |
