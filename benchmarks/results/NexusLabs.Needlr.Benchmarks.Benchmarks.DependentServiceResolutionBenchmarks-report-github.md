```

BenchmarkDotNet v0.14.0, Ubuntu 24.04.4 LTS (Noble Numbat)
Intel Core 7 150U, 1 CPU, 12 logical and 6 physical cores
.NET SDK 10.0.302
  [Host]   : .NET 10.0.10 (10.0.1026.32716), X64 RyuJIT AVX2
  ShortRun : .NET 10.0.10 (10.0.1026.32716), X64 RyuJIT AVX2

Job=ShortRun  IterationCount=5  LaunchCount=1  
WarmupCount=3  

```
| Method                             | Mean      | Error     | StdDev    | Ratio | RatioSD | Rank | Allocated | Alloc Ratio |
|----------------------------------- |----------:|----------:|----------:|------:|--------:|-----:|----------:|------------:|
| ManualDI_ResolveDependent          | 10.454 ns | 0.9352 ns | 0.1447 ns |  1.00 |    0.02 |    1 |         - |          NA |
| Needlr_Reflection_ResolveDependent | 12.239 ns | 4.6719 ns | 1.2133 ns |  1.17 |    0.11 |    1 |         - |          NA |
| Needlr_SourceGen_ResolveDependent  |  9.899 ns | 0.8661 ns | 0.1340 ns |  0.95 |    0.02 |    1 |         - |          NA |
