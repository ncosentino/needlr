```

BenchmarkDotNet v0.14.0, Ubuntu 24.04.4 LTS (Noble Numbat)
AMD EPYC 7763, 1 CPU, 4 logical and 2 physical cores
.NET SDK 10.0.301
  [Host]   : .NET 10.0.9 (10.0.926.27113), X64 RyuJIT AVX2
  ShortRun : .NET 10.0.9 (10.0.926.27113), X64 RyuJIT AVX2

Job=ShortRun  IterationCount=5  LaunchCount=1  
WarmupCount=3  

```
| Method                           | Mean     | Error    | StdDev   | Ratio | Rank | Allocated | Alloc Ratio |
|--------------------------------- |---------:|---------:|---------:|------:|-----:|----------:|------------:|
| ManualDI_ResolveOptions          | 16.95 ns | 0.067 ns | 0.010 ns |  1.00 |    1 |         - |          NA |
| Needlr_Reflection_ResolveOptions | 16.74 ns | 0.171 ns | 0.044 ns |  0.99 |    1 |         - |          NA |
| Needlr_SourceGen_ResolveOptions  | 16.63 ns | 0.068 ns | 0.011 ns |  0.98 |    1 |         - |          NA |
