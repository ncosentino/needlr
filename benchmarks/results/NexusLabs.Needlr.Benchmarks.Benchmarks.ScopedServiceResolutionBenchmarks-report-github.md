```

BenchmarkDotNet v0.14.0, Ubuntu 24.04.3 LTS (Noble Numbat)
AMD EPYC 7763, 1 CPU, 4 logical and 2 physical cores
.NET SDK 10.0.102
  [Host]   : .NET 10.0.2 (10.0.225.61305), X64 RyuJIT AVX2
  ShortRun : .NET 10.0.2 (10.0.225.61305), X64 RyuJIT AVX2

Job=ShortRun  IterationCount=5  LaunchCount=1  
WarmupCount=3  

```
| Method                           | Mean     | Error     | StdDev    | Ratio | Rank | Gen0   | Allocated | Alloc Ratio |
|--------------------------------- |---------:|----------:|----------:|------:|-----:|-------:|----------:|------------:|
| CreateScopeAndResolve_Reflection | 2.150 μs | 0.0514 μs | 0.0079 μs |  1.00 |    1 | 0.0229 |     408 B |        1.00 |
| CreateScopeAndResolve_SourceGen  | 2.235 μs | 0.0246 μs | 0.0064 μs |  1.04 |    1 | 0.0229 |     408 B |        1.00 |
