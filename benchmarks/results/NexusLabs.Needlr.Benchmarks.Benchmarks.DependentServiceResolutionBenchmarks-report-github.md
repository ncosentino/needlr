```

BenchmarkDotNet v0.14.0, Ubuntu 24.04.4 LTS (Noble Numbat)
AMD EPYC 7763, 1 CPU, 4 logical and 2 physical cores
.NET SDK 10.0.203
  [Host]   : .NET 10.0.7 (10.0.726.21808), X64 RyuJIT AVX2
  ShortRun : .NET 10.0.7 (10.0.726.21808), X64 RyuJIT AVX2

Job=ShortRun  IterationCount=5  LaunchCount=1  
WarmupCount=3  

```
| Method                             | Mean     | Error    | StdDev   | Ratio | RatioSD | Rank | Allocated | Alloc Ratio |
|----------------------------------- |---------:|---------:|---------:|------:|--------:|-----:|----------:|------------:|
| ManualDI_ResolveDependent          | 15.81 ns | 2.021 ns | 0.525 ns |  1.00 |    0.04 |    1 |         - |          NA |
| Needlr_Reflection_ResolveDependent | 15.22 ns | 0.364 ns | 0.094 ns |  0.96 |    0.03 |    1 |         - |          NA |
| Needlr_SourceGen_ResolveDependent  | 15.09 ns | 0.276 ns | 0.072 ns |  0.96 |    0.03 |    1 |         - |          NA |
