```

BenchmarkDotNet v0.14.0, Ubuntu 24.04.4 LTS (Noble Numbat)
AMD EPYC 9V74, 1 CPU, 4 logical and 2 physical cores
.NET SDK 10.0.302
  [Host]   : .NET 10.0.10 (10.0.1026.32716), X64 RyuJIT AVX2
  ShortRun : .NET 10.0.10 (10.0.1026.32716), X64 RyuJIT AVX2

Job=ShortRun  IterationCount=5  LaunchCount=1  
WarmupCount=3  

```
| Method                              | Mean     | Error    | StdDev   | Ratio | Rank | Gen0   | Allocated | Alloc Ratio |
|------------------------------------ |---------:|---------:|---------:|------:|-----:|-------:|----------:|------------:|
| ManualDI_ResolveCollection          | 47.40 ns | 0.791 ns | 0.122 ns |  1.00 |    1 | 0.0029 |      48 B |        1.00 |
| Needlr_Reflection_ResolveCollection | 48.99 ns | 0.488 ns | 0.076 ns |  1.03 |    1 | 0.0029 |      48 B |        1.00 |
| Needlr_SourceGen_ResolveCollection  | 49.91 ns | 1.217 ns | 0.188 ns |  1.05 |    1 | 0.0029 |      48 B |        1.00 |
