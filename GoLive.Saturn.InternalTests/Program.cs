using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using GoLive.Saturn.Data;
using GoLive.Saturn.Data.Abstractions;
using GoLive.Saturn.Data.Benchmarks;
using GoLive.Saturn.Data.Entities;

namespace GoLive.Saturn.InternalTests
{
    class Program
    {
        static async Task Main(string[] args)
        {
            
            NullIdInsertTest niit = new NullIdInsertTest();
            await niit.Run();

            //WatchTests test = new WatchTests();
            //await test.Run();
            Console.WriteLine("Hello World!");
        }
    }

    internal class NullIdInsertTest
    {
        public async Task Run()
        {
            Repository repository = new Repository(new RepositoryOptions() { ConnectionString = "mongodb://localhost/GoLiveSaturn" });
            
            //Repository repos = new Repository(new RepositoryOptions()
            //{
            //    ConnectionString = "mongodb://localhost/benchmarktest"
            //}, new TestMongoClient());

            List<TestEntity> entities = new List<TestEntity>();
            entities.Add(new TestEntity{Name = "Entity 1 " + DateTime.Now.ToString("O")});
            entities.Add(new TestEntity{Name = "Entity 2 " + DateTime.Now.ToString("O")});
            entities.Add(new TestEntity{Name = "Entity 3 " + DateTime.Now.ToString("O")});
            entities.Add(new TestEntity{Name = "Entity 4 " + DateTime.Now.ToString("O")});
            entities.Add(new TestEntity{Name = "Entity 5 " + DateTime.Now.ToString("O")});


            await repository.UpsertMany(entities);
            //await repository.Add(entity);
        }
    }

    public class TestEntity : Entity
    {
        public string Name { get; set; }
    }
}
