```

BenchmarkDotNet v0.14.0, Ubuntu 24.04.4 LTS (Noble Numbat)
12th Gen Intel Core i7-12700H, 1 CPU, 20 logical and 10 physical cores
.NET SDK 10.0.302
  [Host]   : .NET 10.0.10 (10.0.1026.32716), X64 RyuJIT AVX2
  ShortRun : .NET 10.0.10 (10.0.1026.32716), X64 RyuJIT AVX2

Job=ShortRun  IterationCount=5  LaunchCount=1  
WarmupCount=3  

```
| Method                              | Mean     | Error     | StdDev   | Ratio | RatioSD | Rank | Gen0   | Allocated | Alloc Ratio |
|------------------------------------ |---------:|----------:|---------:|------:|--------:|-----:|-------:|----------:|------------:|
| ManualDI_ResolveCollection          | 33.14 ns |  6.762 ns | 1.046 ns |  1.00 |    0.04 |    1 | 0.0038 |      48 B |        1.00 |
| Needlr_Reflection_ResolveCollection | 34.22 ns |  9.965 ns | 1.542 ns |  1.03 |    0.05 |    1 | 0.0038 |      48 B |        1.00 |
| Needlr_SourceGen_ResolveCollection  | 37.81 ns | 14.876 ns | 3.863 ns |  1.14 |    0.11 |    1 | 0.0038 |      48 B |        1.00 |
