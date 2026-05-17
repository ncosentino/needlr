```

BenchmarkDotNet v0.14.0, Ubuntu 24.04.4 LTS (Noble Numbat)
AMD EPYC 9V74, 1 CPU, 4 logical and 2 physical cores
.NET SDK 10.0.300
  [Host]   : .NET 10.0.8 (10.0.826.23019), X64 RyuJIT AVX2
  ShortRun : .NET 10.0.8 (10.0.826.23019), X64 RyuJIT AVX2

Job=ShortRun  IterationCount=5  LaunchCount=1  
WarmupCount=3  

```
| Method                             | Mean     | Error    | StdDev   | Ratio | Rank | Allocated | Alloc Ratio |
|----------------------------------- |---------:|---------:|---------:|------:|-----:|----------:|------------:|
| ManualDI_ResolveDeepChain          | 15.38 ns | 0.138 ns | 0.036 ns |  1.00 |    1 |         - |          NA |
| Needlr_Reflection_ResolveDeepChain | 19.33 ns | 0.105 ns | 0.016 ns |  1.26 |    2 |         - |          NA |
| Needlr_SourceGen_ResolveDeepChain  | 14.79 ns | 0.058 ns | 0.015 ns |  0.96 |    1 |         - |          NA |
