```

BenchmarkDotNet v0.14.0, Ubuntu 24.04.4 LTS (Noble Numbat)
AMD EPYC 9V74, 1 CPU, 4 logical and 2 physical cores
.NET SDK 10.0.202
  [Host]   : .NET 10.0.6 (10.0.626.17701), X64 RyuJIT AVX2
  ShortRun : .NET 10.0.6 (10.0.626.17701), X64 RyuJIT AVX2

Job=ShortRun  IterationCount=5  LaunchCount=1  
WarmupCount=3  

```
| Method                             | Mean     | Error    | StdDev   | Ratio | RatioSD | Rank | Allocated | Alloc Ratio |
|----------------------------------- |---------:|---------:|---------:|------:|--------:|-----:|----------:|------------:|
| ManualDI_ResolveDependent          | 19.37 ns | 1.385 ns | 0.360 ns |  1.00 |    0.02 |    1 |         - |          NA |
| Needlr_Reflection_ResolveDependent | 19.03 ns | 0.376 ns | 0.098 ns |  0.98 |    0.02 |    1 |         - |          NA |
| Needlr_SourceGen_ResolveDependent  | 19.62 ns | 0.321 ns | 0.083 ns |  1.01 |    0.02 |    1 |         - |          NA |
