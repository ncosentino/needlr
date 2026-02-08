```

BenchmarkDotNet v0.14.0, Ubuntu 24.04.3 LTS (Noble Numbat)
AMD EPYC 7763, 1 CPU, 4 logical and 2 physical cores
.NET SDK 10.0.102
  [Host]   : .NET 10.0.2 (10.0.225.61305), X64 RyuJIT AVX2
  ShortRun : .NET 10.0.2 (10.0.225.61305), X64 RyuJIT AVX2

Job=ShortRun  IterationCount=5  LaunchCount=1  
WarmupCount=3  

```
| Method                              | Mean     | Error    | StdDev   | Ratio | RatioSD | Rank | Gen0   | Allocated | Alloc Ratio |
|------------------------------------ |---------:|---------:|---------:|------:|--------:|-----:|-------:|----------:|------------:|
| ManualDI_ResolveCollection          | 51.77 ns | 2.474 ns | 0.642 ns |  1.00 |    0.02 |    1 | 0.0029 |      48 B |        1.00 |
| Needlr_Reflection_ResolveCollection | 47.92 ns | 2.488 ns | 0.646 ns |  0.93 |    0.02 |    1 | 0.0029 |      48 B |        1.00 |
| Needlr_SourceGen_ResolveCollection  | 45.46 ns | 1.484 ns | 0.230 ns |  0.88 |    0.01 |    1 | 0.0029 |      48 B |        1.00 |
