```

BenchmarkDotNet v0.14.0, Ubuntu 24.04.4 LTS (Noble Numbat)
AMD EPYC 7763, 1 CPU, 4 logical and 2 physical cores
.NET SDK 10.0.302
  [Host]   : .NET 10.0.11 (10.0.1126.37416), X64 RyuJIT AVX2
  ShortRun : .NET 10.0.11 (10.0.1126.37416), X64 RyuJIT AVX2

Job=ShortRun  IterationCount=5  LaunchCount=1  
WarmupCount=3  

```
| Method                              | Mean     | Error    | StdDev   | Ratio | Rank | Gen0   | Allocated | Alloc Ratio |
|------------------------------------ |---------:|---------:|---------:|------:|-----:|-------:|----------:|------------:|
| ManualDI_ResolveCollection          | 40.54 ns | 0.589 ns | 0.091 ns |  1.00 |    1 | 0.0029 |      48 B |        1.00 |
| Needlr_Reflection_ResolveCollection | 40.56 ns | 1.612 ns | 0.419 ns |  1.00 |    1 | 0.0029 |      48 B |        1.00 |
| Needlr_SourceGen_ResolveCollection  | 44.32 ns | 1.118 ns | 0.290 ns |  1.09 |    1 | 0.0029 |      48 B |        1.00 |
