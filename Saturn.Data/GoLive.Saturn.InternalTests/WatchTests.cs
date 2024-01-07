using System;
using System.Threading;
using System.Threading.Tasks;
using GoLive.Saturn.Data;
using GoLive.Saturn.Data.Abstractions;
using GoLive.Saturn.Data.Entities;

namespace GoLive.Saturn.InternalTests
{
    public class WatchTests
    {
        public async Task Run()
        {
            Repository repository = new Repository(new RepositoryOptions() { ConnectionString = "mongodb://localhost/GoLiveSaturn" });

            

            new Thread(() =>
            {
                Console.WriteLine("Waiting...");
                repository.Watch<TestEntity>(e => e.FullDocument.Name == "Test123", ChangeOperation.Insert,
                    (entity, s, arg3) =>
                    {
                        Console.WriteLine($"Operation: {arg3}, Id={s}, Entity text: {entity.Name}");
                    }).Wait();
            }).Start();
            Thread.Sleep(1000);
            await repository.Insert(new TestEntity() { Name = "Test123" });

        }
    }

    public class StringEmptyInsertTest : Entity
    {
        public string Value1 { get; set; }
        public string Value2 { get; set; }
    }
}