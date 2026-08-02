```

BenchmarkDotNet v0.14.0, Ubuntu 24.04.4 LTS (Noble Numbat)
12th Gen Intel Core i7-12700H, 1 CPU, 20 logical and 10 physical cores
.NET SDK 10.0.302
  [Host]   : .NET 10.0.10 (10.0.1026.32716), X64 RyuJIT AVX2
  ShortRun : .NET 10.0.10 (10.0.1026.32716), X64 RyuJIT AVX2

Job=ShortRun  IterationCount=5  LaunchCount=1  
WarmupCount=3  

```
| Method                          | Mean      | Error    | StdDev    | Ratio | RatioSD | Rank | Allocated | Alloc Ratio |
|-------------------------------- |----------:|---------:|----------:|------:|--------:|-----:|----------:|------------:|
| ManualDI_ResolveSimple          |  9.971 ns | 1.652 ns | 0.2557 ns |  1.00 |    0.03 |    1 |         - |          NA |
| Needlr_Reflection_ResolveSimple | 11.484 ns | 3.186 ns | 0.4930 ns |  1.15 |    0.05 |    1 |         - |          NA |
| Needlr_SourceGen_ResolveSimple  | 10.146 ns | 1.419 ns | 0.3686 ns |  1.02 |    0.04 |    1 |         - |          NA |
