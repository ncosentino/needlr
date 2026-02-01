
BenchmarkDotNet v0.14.0, Ubuntu 24.04.3 LTS (Noble Numbat)
AMD EPYC 7763, 1 CPU, 4 logical and 2 physical cores
.NET SDK 10.0.102
  [Host]   : .NET 10.0.2 (10.0.225.61305), X64 RyuJIT AVX2
  ShortRun : .NET 10.0.2 (10.0.225.61305), X64 RyuJIT AVX2

Job=ShortRun  InvocationCount=1  IterationCount=5  
LaunchCount=1  UnrollFactor=1  WarmupCount=3  

 Method                        | Mean     | Error     | StdDev    | Ratio | Rank | Allocated  | Alloc Ratio |
------------------------------ |---------:|----------:|----------:|------:|-----:|-----------:|------------:|
 BuildWebApp_Reflection        | 8.608 ms | 0.1923 ms | 0.0499 ms |  1.00 |    2 | 1671.91 KB |        1.00 |
 BuildWebApp_SourceGen         | 3.392 ms | 0.3815 ms | 0.0590 ms |  0.39 |    1 |   734.5 KB |        0.44 |
 BuildWebApp_SourceGenExplicit | 3.342 ms | 0.1517 ms | 0.0394 ms |  0.39 |    1 |  674.66 KB |        0.40 |
