// See https://aka.ms/new-console-template for more information
using BenchmarkDotNet.Running;
using DBCD.Benchmark.Benchmarks;

BenchmarkRunner.Run<WritingBenchmark>();