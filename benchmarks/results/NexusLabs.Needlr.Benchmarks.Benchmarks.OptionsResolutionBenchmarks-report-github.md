```

BenchmarkDotNet v0.14.0, Ubuntu 24.04.4 LTS (Noble Numbat)
AMD EPYC 9V74, 1 CPU, 4 logical and 2 physical cores
.NET SDK 10.0.300
  [Host]   : .NET 10.0.8 (10.0.826.23019), X64 RyuJIT AVX2
  ShortRun : .NET 10.0.8 (10.0.826.23019), X64 RyuJIT AVX2

Job=ShortRun  IterationCount=5  LaunchCount=1  
WarmupCount=3  

```
| Method                           | Mean     | Error    | StdDev   | Ratio | Rank | Allocated | Alloc Ratio |
|--------------------------------- |---------:|---------:|---------:|------:|-----:|----------:|------------:|
| ManualDI_ResolveOptions          | 20.97 ns | 0.054 ns | 0.008 ns |  1.00 |    1 |         - |          NA |
| Needlr_Reflection_ResolveOptions | 21.03 ns | 0.050 ns | 0.013 ns |  1.00 |    1 |         - |          NA |
| Needlr_SourceGen_ResolveOptions  | 21.21 ns | 0.114 ns | 0.018 ns |  1.01 |    1 |         - |          NA |
