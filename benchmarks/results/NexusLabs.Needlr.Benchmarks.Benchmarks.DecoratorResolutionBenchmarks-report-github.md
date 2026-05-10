```

BenchmarkDotNet v0.14.0, Ubuntu 24.04.4 LTS (Noble Numbat)
AMD EPYC 9V74, 1 CPU, 4 logical and 2 physical cores
.NET SDK 10.0.203
  [Host]   : .NET 10.0.7 (10.0.726.21808), X64 RyuJIT AVX2
  ShortRun : .NET 10.0.7 (10.0.726.21808), X64 RyuJIT AVX2

Job=ShortRun  IterationCount=5  LaunchCount=1  
WarmupCount=3  

```
| Method                             | Mean     | Error    | StdDev   | Ratio | Rank | Allocated | Alloc Ratio |
|----------------------------------- |---------:|---------:|---------:|------:|-----:|----------:|------------:|
| ManualDI_ResolveDecorated          | 15.62 ns | 0.516 ns | 0.134 ns |  1.00 |    1 |         - |          NA |
| Needlr_Reflection_ResolveDecorated | 19.05 ns | 0.662 ns | 0.172 ns |  1.22 |    1 |         - |          NA |
| Needlr_SourceGen_ResolveDecorated  | 19.15 ns | 0.716 ns | 0.186 ns |  1.23 |    1 |         - |          NA |
