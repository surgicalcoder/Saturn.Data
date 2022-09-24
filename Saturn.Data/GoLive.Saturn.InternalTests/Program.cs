using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using System.Threading.Tasks.Dataflow;
using GoLive.Saturn.Data;
using GoLive.Saturn.Data.Abstractions;
using GoLive.Saturn.Data.Entities;

namespace GoLive.Saturn.InternalTests
{
    class Program
    {
        static async Task Main(string[] args)
        {
            ScopedTests scopedTests = new ScopedTests();
            await scopedTests.Run();
            //Repository repos = new Repository(new RepositoryOptions(){ConnectionString =  "mongodb://localhost/GoLiveSaturn"});



            //List<TestEntity> entities = new List<TestEntity>();
            //entities.Add(new TestEntity { Name = "Entity 1 " + DateTime.Now.ToString("O") });
            //entities.Add(new TestEntity { Name = "Entity 2 " + DateTime.Now.ToString("O") });
            //entities.Add(new TestEntity { Name = "Entity 3 " + DateTime.Now.ToString("O") });
            //entities.Add(new TestEntity { Name = "Entity 4 " + DateTime.Now.ToString("O") });
            //entities.Add(new TestEntity { Name = "Entity 5 " + DateTime.Now.ToString("O") });





            //await repos.Add(new TestEntity3() {Name = "WibbleWobble", Properties = new Dictionary<string, dynamic>()});
            //var wobble = new TestEntity3();
            //wobble.Name = "wobble";
            //wobble.Properties = new Dictionary<string, dynamic>();
            //wobble.Properties.Add("Test.key", 232);
            //wobble.Properties.Add("test.key3", entities);
            //await repos.Add(wobble);


            //Repository repository = new Repository(new RepositoryOptions() { ConnectionString = "mongodb://localhost/GoLiveSaturn" });

            //StringEmptyInsertTest seit = new StringEmptyInsertTest();
            //seit.Value1 = "Test 1";
            //seit.Value2 = "      ";
            //await repository.UpsertMany(new List<StringEmptyInsertTest> {seit});

            //await repository.Add(seit);

            //NullIdInsertTest niit = new NullIdInsertTest();
            //await niit.Run();

            ////WatchTests test = new WatchTests();
            ////await test.Run();
            //Console.WriteLine("Hello World!");
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

    public class TestEntity3 : Entity
    {
        public string Name { get; set; }
    }

    public class TestEntity : Entity
    {
        public string Name { get; set; }
    }
}
