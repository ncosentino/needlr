
BenchmarkDotNet v0.14.0, Ubuntu 24.04.4 LTS (Noble Numbat)
AMD EPYC 9V74, 1 CPU, 4 logical and 2 physical cores
.NET SDK 10.0.202
  [Host]   : .NET 10.0.6 (10.0.626.17701), X64 RyuJIT AVX2
  ShortRun : .NET 10.0.6 (10.0.626.17701), X64 RyuJIT AVX2

Job=ShortRun  IterationCount=5  LaunchCount=1  
WarmupCount=3  

 Method                              | Mean     | Error    | StdDev   | Ratio | Rank | Gen0   | Allocated | Alloc Ratio |
------------------------------------ |---------:|---------:|---------:|------:|-----:|-------:|----------:|------------:|
 ManualDI_ResolveCollection          | 44.70 ns | 0.862 ns | 0.224 ns |  1.00 |    1 | 0.0029 |      48 B |        1.00 |
 Needlr_Reflection_ResolveCollection | 50.97 ns | 1.122 ns | 0.291 ns |  1.14 |    1 | 0.0029 |      48 B |        1.00 |
 Needlr_SourceGen_ResolveCollection  | 47.70 ns | 0.361 ns | 0.056 ns |  1.07 |    1 | 0.0029 |      48 B |        1.00 |
