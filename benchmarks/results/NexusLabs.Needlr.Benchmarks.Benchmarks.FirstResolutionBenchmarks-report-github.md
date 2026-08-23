```

BenchmarkDotNet v0.14.0, Ubuntu 24.04.4 LTS (Noble Numbat)
AMD EPYC 7763, 1 CPU, 4 logical and 2 physical cores
.NET SDK 10.0.302
  [Host]   : .NET 10.0.11 (10.0.1126.37416), X64 RyuJIT AVX2
  ShortRun : .NET 10.0.11 (10.0.1126.37416), X64 RyuJIT AVX2

Job=ShortRun  InvocationCount=1  IterationCount=5  
LaunchCount=1  UnrollFactor=1  WarmupCount=3  

```
| Method                                 | Mean        | Error     | StdDev    | Ratio  | RatioSD | Rank | Allocated | Alloc Ratio |
|--------------------------------------- |------------:|----------:|----------:|-------:|--------:|-----:|----------:|------------:|
| ManualDI_BuildAndResolveFirst          |    26.08 μs |  0.732 μs |  0.113 μs |   1.00 |    0.01 |    1 |         - |          NA |
| Needlr_Reflection_BuildAndResolveFirst | 5,806.27 μs | 99.458 μs | 25.829 μs | 222.62 |    1.25 |    3 | 1318656 B |          NA |
| Needlr_SourceGen_BuildAndResolveFirst  |   477.55 μs | 73.223 μs | 19.016 μs |  18.31 |    0.67 |    2 |  217768 B |          NA |
