
BenchmarkDotNet v0.14.0, Ubuntu 24.04.4 LTS (Noble Numbat)
AMD EPYC 9V74, 1 CPU, 4 logical and 2 physical cores
.NET SDK 10.0.201
  [Host]   : .NET 10.0.5 (10.0.526.15411), X64 RyuJIT AVX2
  ShortRun : .NET 10.0.5 (10.0.526.15411), X64 RyuJIT AVX2

Job=ShortRun  InvocationCount=1  IterationCount=5  
LaunchCount=1  UnrollFactor=1  WarmupCount=3  

 Method                                 | Mean        | Error     | StdDev    | Ratio  | RatioSD | Rank | Allocated | Alloc Ratio |
--------------------------------------- |------------:|----------:|----------:|-------:|--------:|-----:|----------:|------------:|
 ManualDI_BuildAndResolveFirst          |    27.52 μs |  2.645 μs |  0.409 μs |   1.00 |    0.02 |    1 |         - |          NA |
 Needlr_Reflection_BuildAndResolveFirst | 5,456.72 μs | 77.482 μs | 20.122 μs | 198.28 |    2.70 |    3 | 1319568 B |          NA |
 Needlr_SourceGen_BuildAndResolveFirst  |   413.97 μs | 23.438 μs |  3.627 μs |  15.04 |    0.23 |    2 |  217096 B |          NA |
