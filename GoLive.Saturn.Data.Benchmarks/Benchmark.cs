using System.Collections.Generic;
using BenchmarkDotNet.Attributes;
using GoLive.Saturn.Data.Abstractions;
using GoLive.Saturn.Data.Entities;
using MongoDB.Bson;

namespace GoLive.Saturn.Data.Benchmarks
{
    [ClrJob(baseline: true), CoreJob, CoreRtJob]
    [RPlotExporter, RankColumn]
    [MemoryDiagnoser]
    public class Benchmark
    {
        private Repository repository;

        private List<TestEntity1> entities;

        [GlobalSetup]
        public void Setup()
        {
            entities = new List<TestEntity1>();

            for (int i = 0; i < 1000; i++)
            {
                var item = new TestEntity1();
                item.Counter = i;

                if (i % 3 == 0)
                {
                    item.Id = ObjectId.GenerateNewId().ToString();
                }

                entities.Add(item);
            }

            repository = new Repository(new RepositoryOptions()
            {
                ConnectionString = "mongodb://localhost/benchmarktest"
            }, new TestMongoClient());
        }

        [Benchmark]
        public void ManyTest()
        {
            repository.UpsertMany(entities).Wait();
        }

        [Benchmark]
        public void ManyTestLinq()
        {
            repository.UpsertManyLinq(entities).Wait();
        }

    }


    public class TestEntity1 : Entity
    {
        public int Counter { get; set; }
    }
}