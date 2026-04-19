```

BenchmarkDotNet v0.14.0, Ubuntu 24.04.4 LTS (Noble Numbat)
AMD EPYC 9V74, 1 CPU, 4 logical and 2 physical cores
.NET SDK 10.0.202
  [Host]   : .NET 10.0.6 (10.0.626.17701), X64 RyuJIT AVX2
  ShortRun : .NET 10.0.6 (10.0.626.17701), X64 RyuJIT AVX2

Job=ShortRun  IterationCount=5  LaunchCount=1  
WarmupCount=3  

```
| Method                               | Mean     | Error    | StdDev   | Ratio | Rank | Gen0   | Allocated | Alloc Ratio |
|------------------------------------- |---------:|---------:|---------:|------:|-----:|-------:|----------:|------------:|
| ManualDI_ResolveOpenGeneric          | 23.35 ns | 0.106 ns | 0.016 ns |  1.00 |    1 | 0.0014 |      24 B |        1.00 |
| Needlr_Reflection_ResolveOpenGeneric | 20.50 ns | 0.655 ns | 0.170 ns |  0.88 |    1 | 0.0014 |      24 B |        1.00 |
| Needlr_SourceGen_ResolveOpenGeneric  | 20.23 ns | 0.887 ns | 0.137 ns |  0.87 |    1 | 0.0014 |      24 B |        1.00 |
