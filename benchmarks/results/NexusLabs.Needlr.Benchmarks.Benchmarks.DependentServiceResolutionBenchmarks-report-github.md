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
| ManualDI_ResolveDependent          | 16.41 ns | 0.036 ns | 0.009 ns |  1.00 |    1 |         - |          NA |
| Needlr_Reflection_ResolveDependent | 15.10 ns | 0.227 ns | 0.059 ns |  0.92 |    1 |         - |          NA |
| Needlr_SourceGen_ResolveDependent  | 19.45 ns | 0.367 ns | 0.095 ns |  1.19 |    1 |         - |          NA |
