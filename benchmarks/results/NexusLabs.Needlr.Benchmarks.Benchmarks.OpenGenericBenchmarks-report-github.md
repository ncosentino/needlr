```

BenchmarkDotNet v0.14.0, Ubuntu 24.04.4 LTS (Noble Numbat)
AMD EPYC 9V74, 1 CPU, 4 logical and 2 physical cores
.NET SDK 10.0.302
  [Host]   : .NET 10.0.10 (10.0.1026.32716), X64 RyuJIT AVX2
  ShortRun : .NET 10.0.10 (10.0.1026.32716), X64 RyuJIT AVX2

Job=ShortRun  IterationCount=5  LaunchCount=1  
WarmupCount=3  

```
| Method                               | Mean     | Error    | StdDev   | Ratio | Rank | Gen0   | Allocated | Alloc Ratio |
|------------------------------------- |---------:|---------:|---------:|------:|-----:|-------:|----------:|------------:|
| ManualDI_ResolveOpenGeneric          | 23.34 ns | 0.762 ns | 0.118 ns |  1.00 |    1 | 0.0014 |      24 B |        1.00 |
| Needlr_Reflection_ResolveOpenGeneric | 24.17 ns | 0.227 ns | 0.059 ns |  1.04 |    1 | 0.0014 |      24 B |        1.00 |
| Needlr_SourceGen_ResolveOpenGeneric  | 21.53 ns | 0.608 ns | 0.158 ns |  0.92 |    1 | 0.0014 |      24 B |        1.00 |
