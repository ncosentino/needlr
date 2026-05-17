```

BenchmarkDotNet v0.14.0, Ubuntu 24.04.4 LTS (Noble Numbat)
AMD EPYC 9V74, 1 CPU, 4 logical and 2 physical cores
.NET SDK 10.0.300
  [Host]   : .NET 10.0.8 (10.0.826.23019), X64 RyuJIT AVX2
  ShortRun : .NET 10.0.8 (10.0.826.23019), X64 RyuJIT AVX2

Job=ShortRun  IterationCount=5  LaunchCount=1  
WarmupCount=3  

```
| Method                             | Mean     | Error    | StdDev   | Ratio | RatioSD | Rank | Allocated | Alloc Ratio |
|----------------------------------- |---------:|---------:|---------:|------:|--------:|-----:|----------:|------------:|
| ManualDI_ResolveDecorated          | 16.18 ns | 0.876 ns | 0.228 ns |  1.00 |    0.02 |    1 |         - |          NA |
| Needlr_Reflection_ResolveDecorated | 19.08 ns | 0.804 ns | 0.124 ns |  1.18 |    0.02 |    1 |         - |          NA |
| Needlr_SourceGen_ResolveDecorated  | 19.17 ns | 0.402 ns | 0.104 ns |  1.19 |    0.02 |    1 |         - |          NA |
