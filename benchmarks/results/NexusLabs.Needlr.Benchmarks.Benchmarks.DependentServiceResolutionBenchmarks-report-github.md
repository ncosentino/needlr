```

BenchmarkDotNet v0.14.0, Ubuntu 24.04.5 LTS (Noble Numbat)
AMD EPYC 9V74, 1 CPU, 4 logical and 2 physical cores
.NET SDK 10.0.303
  [Host]   : .NET 10.0.11 (10.0.1126.37416), X64 RyuJIT AVX-512F+CD+BW+DQ+VL+VBMI
  ShortRun : .NET 10.0.11 (10.0.1126.37416), X64 RyuJIT AVX-512F+CD+BW+DQ+VL+VBMI

Job=ShortRun  IterationCount=5  LaunchCount=1  
WarmupCount=3  

```
| Method                             | Mean     | Error    | StdDev   | Ratio | Rank | Allocated | Alloc Ratio |
|----------------------------------- |---------:|---------:|---------:|------:|-----:|----------:|------------:|
| ManualDI_ResolveDependent          | 12.66 ns | 0.158 ns | 0.041 ns |  1.00 |    1 |         - |          NA |
| Needlr_Reflection_ResolveDependent | 15.69 ns | 0.204 ns | 0.053 ns |  1.24 |    1 |         - |          NA |
| Needlr_SourceGen_ResolveDependent  | 14.52 ns | 0.045 ns | 0.007 ns |  1.15 |    1 |         - |          NA |
