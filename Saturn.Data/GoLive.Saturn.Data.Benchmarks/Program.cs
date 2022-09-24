using System;
using BenchmarkDotNet.Configs;
using BenchmarkDotNet.Running;

namespace GoLive.Saturn.Data.Benchmarks
{
    class Program
    {
        static void Main(string[] args)
        {
            
            
            var summary = BenchmarkRunner.Run<Benchmark>(new DebugInProcessConfig());
            //Console.ReadLine();
        }
    }
}
