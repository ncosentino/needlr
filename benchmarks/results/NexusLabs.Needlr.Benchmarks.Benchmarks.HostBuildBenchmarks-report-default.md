
BenchmarkDotNet v0.14.0, Ubuntu 24.04.3 LTS (Noble Numbat)
AMD EPYC 7763, 1 CPU, 4 logical and 2 physical cores
.NET SDK 10.0.102
  [Host]   : .NET 10.0.2 (10.0.225.61305), X64 RyuJIT AVX2
  ShortRun : .NET 10.0.2 (10.0.225.61305), X64 RyuJIT AVX2

Job=ShortRun  InvocationCount=1  IterationCount=5  
LaunchCount=1  UnrollFactor=1  WarmupCount=3  

 Method                      | Mean     | Error     | StdDev    | Ratio | Rank | Allocated | Alloc Ratio |
---------------------------- |---------:|----------:|----------:|------:|-----:|----------:|------------:|
 BuildHost_Reflection        | 6.625 ms | 0.1319 ms | 0.0343 ms |  1.00 |    2 | 1264.8 KB |        1.00 |
 BuildHost_SourceGen         | 2.681 ms | 0.0956 ms | 0.0148 ms |  0.40 |    1 | 549.16 KB |        0.43 |
 BuildHost_SourceGenExplicit | 2.616 ms | 0.0640 ms | 0.0166 ms |  0.39 |    1 | 484.36 KB |        0.38 |
