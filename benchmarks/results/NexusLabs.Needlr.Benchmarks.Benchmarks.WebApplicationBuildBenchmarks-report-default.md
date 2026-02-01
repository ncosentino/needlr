
BenchmarkDotNet v0.14.0, Ubuntu 24.04.3 LTS (Noble Numbat)
AMD EPYC 7763, 1 CPU, 4 logical and 2 physical cores
.NET SDK 10.0.102
  [Host]   : .NET 10.0.2 (10.0.225.61305), X64 RyuJIT AVX2
  ShortRun : .NET 10.0.2 (10.0.225.61305), X64 RyuJIT AVX2

Job=ShortRun  InvocationCount=1  IterationCount=5  
LaunchCount=1  UnrollFactor=1  WarmupCount=3  

 Method                        | Mean     | Error     | StdDev    | Ratio | Rank | Allocated | Alloc Ratio |
------------------------------ |---------:|----------:|----------:|------:|-----:|----------:|------------:|
 BuildWebApp_Reflection        | 7.229 ms | 0.2504 ms | 0.0388 ms |  1.00 |    2 | 1430.3 KB |        1.00 |
 BuildWebApp_SourceGen         | 3.267 ms | 0.1271 ms | 0.0330 ms |  0.45 |    1 | 734.67 KB |        0.51 |
 BuildWebApp_SourceGenExplicit | 3.221 ms | 0.1339 ms | 0.0348 ms |  0.45 |    1 | 654.54 KB |        0.46 |
