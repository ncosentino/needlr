```

BenchmarkDotNet v0.14.0, Ubuntu 24.04.4 LTS (Noble Numbat)
AMD EPYC 7763, 1 CPU, 4 logical and 2 physical cores
.NET SDK 10.0.301
  [Host]   : .NET 10.0.9 (10.0.926.27113), X64 RyuJIT AVX2
  ShortRun : .NET 10.0.9 (10.0.926.27113), X64 RyuJIT AVX2

Job=ShortRun  IterationCount=5  LaunchCount=1  
WarmupCount=3  

```
| Method                             | Mean     | Error    | StdDev   | Ratio | RatioSD | Rank | Allocated | Alloc Ratio |
|----------------------------------- |---------:|---------:|---------:|------:|--------:|-----:|----------:|------------:|
| ManualDI_ResolveDependent          | 15.65 ns | 1.559 ns | 0.405 ns |  1.00 |    0.03 |    1 |         - |          NA |
| Needlr_Reflection_ResolveDependent | 15.31 ns | 0.063 ns | 0.010 ns |  0.98 |    0.02 |    1 |         - |          NA |
| Needlr_SourceGen_ResolveDependent  | 15.13 ns | 0.465 ns | 0.121 ns |  0.97 |    0.02 |    1 |         - |          NA |
