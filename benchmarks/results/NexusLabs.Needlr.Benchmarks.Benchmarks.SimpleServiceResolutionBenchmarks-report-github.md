```

BenchmarkDotNet v0.14.0, Ubuntu 24.04.4 LTS (Noble Numbat)
AMD EPYC 9V74, 1 CPU, 4 logical and 2 physical cores
.NET SDK 10.0.300
  [Host]   : .NET 10.0.8 (10.0.826.23019), X64 RyuJIT AVX2
  ShortRun : .NET 10.0.8 (10.0.826.23019), X64 RyuJIT AVX2

Job=ShortRun  IterationCount=5  LaunchCount=1  
WarmupCount=3  

```
| Method                          | Mean     | Error    | StdDev   | Ratio | Rank | Allocated | Alloc Ratio |
|-------------------------------- |---------:|---------:|---------:|------:|-----:|----------:|------------:|
| ManualDI_ResolveSimple          | 19.01 ns | 0.074 ns | 0.011 ns |  1.00 |    1 |         - |          NA |
| Needlr_Reflection_ResolveSimple | 18.64 ns | 0.515 ns | 0.080 ns |  0.98 |    1 |         - |          NA |
| Needlr_SourceGen_ResolveSimple  | 19.34 ns | 0.390 ns | 0.101 ns |  1.02 |    1 |         - |          NA |
