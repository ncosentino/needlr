```

BenchmarkDotNet v0.14.0, Ubuntu 24.04.3 LTS (Noble Numbat)
AMD EPYC 7763, 1 CPU, 4 logical and 2 physical cores
.NET SDK 10.0.102
  [Host]   : .NET 10.0.2 (10.0.225.61305), X64 RyuJIT AVX2
  ShortRun : .NET 10.0.2 (10.0.225.61305), X64 RyuJIT AVX2

Job=ShortRun  IterationCount=5  LaunchCount=1  
WarmupCount=3  

```
| Method                           | Mean     | Error    | StdDev   | Ratio | Rank | Allocated | Alloc Ratio |
|--------------------------------- |---------:|---------:|---------:|------:|-----:|----------:|------------:|
| ManualDI_ResolveOptions          | 16.92 ns | 0.161 ns | 0.025 ns |  1.00 |    1 |         - |          NA |
| Needlr_Reflection_ResolveOptions | 17.95 ns | 0.103 ns | 0.027 ns |  1.06 |    1 |         - |          NA |
| Needlr_SourceGen_ResolveOptions  | 17.38 ns | 0.535 ns | 0.139 ns |  1.03 |    1 |         - |          NA |
