```

BenchmarkDotNet v0.14.0, Ubuntu 24.04.4 LTS (Noble Numbat)
12th Gen Intel Core i7-12700H, 1 CPU, 20 logical and 10 physical cores
.NET SDK 10.0.302
  [Host]   : .NET 10.0.10 (10.0.1026.32716), X64 RyuJIT AVX2
  ShortRun : .NET 10.0.10 (10.0.1026.32716), X64 RyuJIT AVX2

Job=ShortRun  IterationCount=5  LaunchCount=1  
WarmupCount=3  

```
| Method                          | Mean         | Error         | StdDev       | Ratio    | RatioSD | Rank | Gen0    | Gen1   | Allocated | Alloc Ratio |
|-------------------------------- |-------------:|--------------:|-------------:|---------:|--------:|-----:|--------:|-------:|----------:|------------:|
| ManualDI_RegisterTypes          |     104.0 ns |      27.39 ns |      7.11 ns |     1.00 |    0.09 |    1 |  0.0421 |      - |     528 B |        1.00 |
| Needlr_Reflection_RegisterTypes | 951,135.7 ns | 209,532.33 ns | 32,425.36 ns | 9,181.18 |  647.64 |    3 | 46.8750 | 3.9063 |  591591 B |    1,120.44 |
| Needlr_SourceGen_RegisterTypes  |  12,531.7 ns |   1,516.12 ns |    393.73 ns |   120.97 |    8.41 |    2 |  2.4567 | 0.1984 |   30960 B |       58.64 |
