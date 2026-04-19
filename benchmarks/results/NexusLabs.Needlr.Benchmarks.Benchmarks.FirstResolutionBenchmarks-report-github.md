```

BenchmarkDotNet v0.14.0, Ubuntu 24.04.4 LTS (Noble Numbat)
AMD EPYC 9V74, 1 CPU, 4 logical and 2 physical cores
.NET SDK 10.0.202
  [Host]   : .NET 10.0.6 (10.0.626.17701), X64 RyuJIT AVX2
  ShortRun : .NET 10.0.6 (10.0.626.17701), X64 RyuJIT AVX2

Job=ShortRun  InvocationCount=1  IterationCount=5  
LaunchCount=1  UnrollFactor=1  WarmupCount=3  

```
| Method                                 | Mean        | Error      | StdDev    | Ratio  | RatioSD | Rank | Allocated | Alloc Ratio |
|--------------------------------------- |------------:|-----------:|----------:|-------:|--------:|-----:|----------:|------------:|
| ManualDI_BuildAndResolveFirst          |    35.46 μs |   6.455 μs |  0.999 μs |   1.00 |    0.04 |    1 |         - |          NA |
| Needlr_Reflection_BuildAndResolveFirst | 5,515.13 μs | 230.711 μs | 59.915 μs | 155.63 |    4.19 |    3 | 1319568 B |          NA |
| Needlr_SourceGen_BuildAndResolveFirst  |   472.27 μs | 136.669 μs | 35.493 μs |  13.33 |    0.98 |    2 |  217096 B |          NA |
