```

BenchmarkDotNet v0.14.0, Ubuntu 24.04.4 LTS (Noble Numbat)
Intel Core 7 150U, 1 CPU, 12 logical and 6 physical cores
.NET SDK 10.0.302
  [Host]   : .NET 10.0.10 (10.0.1026.32716), X64 RyuJIT AVX2
  ShortRun : .NET 10.0.10 (10.0.1026.32716), X64 RyuJIT AVX2

Job=ShortRun  IterationCount=5  LaunchCount=1  
WarmupCount=3  

```
| Method                              | Mean      | Error     | StdDev    | Ratio | RatioSD | Rank | Gen0   | Allocated | Alloc Ratio |
|------------------------------------ |----------:|----------:|----------:|------:|--------:|-----:|-------:|----------:|------------:|
| ManualDI_ResolveCollection          |  51.77 ns |  22.19 ns |  5.763 ns |  1.01 |    0.14 |    1 | 0.0076 |      48 B |        1.00 |
| Needlr_Reflection_ResolveCollection | 101.27 ns |  91.97 ns | 23.883 ns |  1.98 |    0.47 |    2 | 0.0076 |      48 B |        1.00 |
| Needlr_SourceGen_ResolveCollection  |  95.20 ns | 128.59 ns | 33.394 ns |  1.86 |    0.63 |    2 | 0.0076 |      48 B |        1.00 |
