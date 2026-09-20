```

BenchmarkDotNet v0.14.0, Ubuntu 24.04.5 LTS (Noble Numbat)
AMD EPYC 9V74, 1 CPU, 4 logical and 2 physical cores
.NET SDK 10.0.303
  [Host]   : .NET 10.0.11 (10.0.1126.37416), X64 RyuJIT AVX-512F+CD+BW+DQ+VL+VBMI
  ShortRun : .NET 10.0.11 (10.0.1126.37416), X64 RyuJIT AVX-512F+CD+BW+DQ+VL+VBMI

Job=ShortRun  InvocationCount=1  IterationCount=5  
LaunchCount=1  UnrollFactor=1  WarmupCount=3  

```
| Method                                 | Mean        | Error      | StdDev    | Ratio  | RatioSD | Rank | Allocated | Alloc Ratio |
|--------------------------------------- |------------:|-----------:|----------:|-------:|--------:|-----:|----------:|------------:|
| ManualDI_BuildAndResolveFirst          |    23.18 μs |   2.654 μs |  0.411 μs |   1.00 |    0.02 |    1 |         - |          NA |
| Needlr_Reflection_BuildAndResolveFirst | 4,225.03 μs | 138.783 μs | 36.041 μs | 182.34 |    3.23 |    3 | 1319080 B |          NA |
| Needlr_SourceGen_BuildAndResolveFirst  |   321.11 μs |  32.813 μs |  5.078 μs |  13.86 |    0.30 |    2 |  217768 B |          NA |
