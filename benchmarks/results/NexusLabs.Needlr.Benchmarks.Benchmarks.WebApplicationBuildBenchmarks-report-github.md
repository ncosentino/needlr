```

BenchmarkDotNet v0.14.0, Ubuntu 24.04.5 LTS (Noble Numbat)
AMD EPYC 9V74, 1 CPU, 4 logical and 2 physical cores
.NET SDK 10.0.303
  [Host]   : .NET 10.0.11 (10.0.1126.37416), X64 RyuJIT AVX-512F+CD+BW+DQ+VL+VBMI
  ShortRun : .NET 10.0.11 (10.0.1126.37416), X64 RyuJIT AVX-512F+CD+BW+DQ+VL+VBMI

Job=ShortRun  InvocationCount=1  IterationCount=5  
LaunchCount=1  UnrollFactor=1  WarmupCount=3  

```
| Method                               | Mean     | Error     | StdDev    | Ratio | RatioSD | Rank | Allocated  | Alloc Ratio |
|------------------------------------- |---------:|----------:|----------:|------:|--------:|-----:|-----------:|------------:|
| ManualDI_BuildWebApp                 | 2.224 ms | 0.0874 ms | 0.0135 ms |  1.00 |    0.01 |    1 |  519.39 KB |        1.00 |
| Needlr_Reflection_BuildWebApp        | 6.671 ms | 0.1479 ms | 0.0384 ms |  3.00 |    0.02 |    2 | 1777.16 KB |        3.42 |
| Needlr_SourceGen_BuildWebApp         | 2.703 ms | 0.0710 ms | 0.0110 ms |  1.22 |    0.01 |    1 |  800.59 KB |        1.54 |
| Needlr_SourceGenExplicit_BuildWebApp | 2.695 ms | 0.1930 ms | 0.0299 ms |  1.21 |    0.01 |    1 |  723.84 KB |        1.39 |
