```

BenchmarkDotNet v0.14.0, Ubuntu 24.04.4 LTS (Noble Numbat)
AMD EPYC 7763, 1 CPU, 4 logical and 2 physical cores
.NET SDK 10.0.302
  [Host]   : .NET 10.0.11 (10.0.1126.37416), X64 RyuJIT AVX2
  ShortRun : .NET 10.0.11 (10.0.1126.37416), X64 RyuJIT AVX2

Job=ShortRun  IterationCount=5  LaunchCount=1  
WarmupCount=3  

```
| Method                               | Mean     | Error    | StdDev   | Ratio | Rank | Gen0   | Allocated | Alloc Ratio |
|------------------------------------- |---------:|---------:|---------:|------:|-----:|-------:|----------:|------------:|
| ManualDI_ResolveOpenGeneric          | 19.75 ns | 0.058 ns | 0.009 ns |  1.00 |    1 | 0.0014 |      24 B |        1.00 |
| Needlr_Reflection_ResolveOpenGeneric | 19.84 ns | 0.560 ns | 0.146 ns |  1.00 |    1 | 0.0014 |      24 B |        1.00 |
| Needlr_SourceGen_ResolveOpenGeneric  | 19.73 ns | 0.204 ns | 0.053 ns |  1.00 |    1 | 0.0014 |      24 B |        1.00 |
