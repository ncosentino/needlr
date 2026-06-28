```

BenchmarkDotNet v0.14.0, Ubuntu 24.04.4 LTS (Noble Numbat)
AMD EPYC 7763, 1 CPU, 4 logical and 2 physical cores
.NET SDK 10.0.301
  [Host]   : .NET 10.0.9 (10.0.926.27113), X64 RyuJIT AVX2
  ShortRun : .NET 10.0.9 (10.0.926.27113), X64 RyuJIT AVX2

Job=ShortRun  IterationCount=5  LaunchCount=1  
WarmupCount=3  

```
| Method                              | Mean     | Error    | StdDev   | Ratio | Rank | Gen0   | Allocated | Alloc Ratio |
|------------------------------------ |---------:|---------:|---------:|------:|-----:|-------:|----------:|------------:|
| ManualDI_ResolveCollection          | 42.69 ns | 1.496 ns | 0.389 ns |  1.00 |    1 | 0.0029 |      48 B |        1.00 |
| Needlr_Reflection_ResolveCollection | 43.47 ns | 0.272 ns | 0.071 ns |  1.02 |    1 | 0.0029 |      48 B |        1.00 |
| Needlr_SourceGen_ResolveCollection  | 43.43 ns | 0.943 ns | 0.146 ns |  1.02 |    1 | 0.0029 |      48 B |        1.00 |
