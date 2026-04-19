```

BenchmarkDotNet v0.14.0, Ubuntu 24.04.4 LTS (Noble Numbat)
AMD EPYC 9V74, 1 CPU, 4 logical and 2 physical cores
.NET SDK 10.0.202
  [Host]   : .NET 10.0.6 (10.0.626.17701), X64 RyuJIT AVX2
  ShortRun : .NET 10.0.6 (10.0.626.17701), X64 RyuJIT AVX2

Job=ShortRun  IterationCount=5  LaunchCount=1  
WarmupCount=3  

```
| Method                           | Mean     | Error    | StdDev   | Ratio | Rank | Allocated | Alloc Ratio |
|--------------------------------- |---------:|---------:|---------:|------:|-----:|----------:|------------:|
| ManualDI_ResolveOptions          | 21.29 ns | 0.060 ns | 0.015 ns |  1.00 |    1 |         - |          NA |
| Needlr_Reflection_ResolveOptions | 22.53 ns | 0.143 ns | 0.022 ns |  1.06 |    1 |         - |          NA |
| Needlr_SourceGen_ResolveOptions  | 21.86 ns | 0.318 ns | 0.082 ns |  1.03 |    1 |         - |          NA |
