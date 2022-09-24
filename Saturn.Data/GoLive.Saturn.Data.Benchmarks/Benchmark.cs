using System.Collections.Generic;
using BenchmarkDotNet.Attributes;
using GoLive.Saturn.Data.Abstractions;
using GoLive.Saturn.Data.Entities;
using MongoDB.Bson;

namespace GoLive.Saturn.Data.Benchmarks
{
    [CoreJob]
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

        //[Benchmark]
        //public void ManyTest()
        //{
        //    repository.UpsertMany(entities).Wait();
        //}

        [Benchmark]
        public void NameWithDotTest()
        {
            repository.GetCollectionNameForType<string>("This.Is.A.Test");
        }

        [Benchmark]
        public void ClassName()
        {
            repository.GetCollectionNameForType<TestEntity1>(null);
        }

        [Benchmark]
        public void GenericClassName()
        {
            repository.GetCollectionNameForType<TestEntity2<string>>(null);
        }


 //[Benchmark]
 //       public void NameWithDotTest2()
 //       {
 //           repository.GetCollectionNameForType2<string>("This.Is.A.Test");
 //       }

 //       [Benchmark]
 //       public void ClassName2()
 //       {
 //           repository.GetCollectionNameForType2<TestEntity1>(null);
 //       }

 //       [Benchmark]
 //       public void GenericClassName2()
 //       {
 //           repository.GetCollectionNameForType2<TestEntity2<string>>(null);
 //       }


        
    }


    public class TestEntity1 : Entity
    {
        public int Counter { get; set; }
    }

    public class TestEntity2<T> : Entity
    {
        public T Key { get; set; }
    }
}