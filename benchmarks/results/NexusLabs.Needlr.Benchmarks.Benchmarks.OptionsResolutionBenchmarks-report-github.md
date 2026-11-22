```

BenchmarkDotNet v0.14.0, Ubuntu 24.04.5 LTS (Noble Numbat)
AMD EPYC 9V74, 1 CPU, 4 logical and 2 physical cores
.NET SDK 10.0.404
  [Host]   : .NET 10.0.15 (10.0.1526.53008), X64 RyuJIT AVX-512F+CD+BW+DQ+VL+VBMI
  ShortRun : .NET 10.0.15 (10.0.1526.53008), X64 RyuJIT AVX-512F+CD+BW+DQ+VL+VBMI

Job=ShortRun  IterationCount=5  LaunchCount=1  
WarmupCount=3  

```
| Method                           | Mean     | Error    | StdDev   | Ratio | Rank | Allocated | Alloc Ratio |
|--------------------------------- |---------:|---------:|---------:|------:|-----:|----------:|------------:|
| ManualDI_ResolveOptions          | 18.88 ns | 0.245 ns | 0.064 ns |  1.00 |    1 |         - |          NA |
| Needlr_Reflection_ResolveOptions | 17.42 ns | 1.035 ns | 0.269 ns |  0.92 |    1 |         - |          NA |
| Needlr_SourceGen_ResolveOptions  | 18.72 ns | 0.044 ns | 0.011 ns |  0.99 |    1 |         - |          NA |
