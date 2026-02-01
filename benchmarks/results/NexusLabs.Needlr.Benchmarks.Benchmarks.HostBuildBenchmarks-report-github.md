```

BenchmarkDotNet v0.14.0, Ubuntu 24.04.3 LTS (Noble Numbat)
AMD EPYC 7763, 1 CPU, 4 logical and 2 physical cores
.NET SDK 10.0.102
  [Host]   : .NET 10.0.2 (10.0.225.61305), X64 RyuJIT AVX2
  ShortRun : .NET 10.0.2 (10.0.225.61305), X64 RyuJIT AVX2

Job=ShortRun  InvocationCount=1  IterationCount=5  
LaunchCount=1  UnrollFactor=1  WarmupCount=3  

```
| Method                      | Mean     | Error     | StdDev    | Ratio | Rank | Allocated  | Alloc Ratio |
|---------------------------- |---------:|----------:|----------:|------:|-----:|-----------:|------------:|
| BuildHost_Reflection        | 7.986 ms | 0.2624 ms | 0.0682 ms |  1.00 |    2 | 1500.02 KB |        1.00 |
| BuildHost_SourceGen         | 2.764 ms | 0.1062 ms | 0.0276 ms |  0.35 |    1 |  565.88 KB |        0.38 |
| BuildHost_SourceGenExplicit | 2.706 ms | 0.0936 ms | 0.0243 ms |  0.34 |    1 |  494.53 KB |        0.33 |
