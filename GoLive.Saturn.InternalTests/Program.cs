using System;
using System.Threading;
using System.Threading.Tasks;
using GoLive.Saturn.Data;
using GoLive.Saturn.Data.Abstractions;
using GoLive.Saturn.Data.Entities;

namespace GoLive.Saturn.InternalTests
{
    class Program
    {
        static async Task Main(string[] args)
        {
            Repository repository = new Repository(new RepositoryOptions(){ConnectionString = "mongodb://localhost/GoLiveSaturn" });

            new Thread(() =>
            {
                Console.WriteLine("Waiting...");
                repository.Watch<TestEntity>(e => e.FullDocument.Name == "Test123", Repository.ChangeOperation.Insert,
                    (entity, s, arg3) =>
                    {
                        Console.WriteLine($"Operation: {arg3}, Id={s}, Entity text: {entity.Name}");
                    }).Wait(); 
            }).Start();
            Thread.Sleep(1000);
            await repository.Add(new TestEntity() {Name = "Test123"});
            //repository.Watch<TestEntity>(e => e.Name == "Test123");
            
            Console.WriteLine("Hello World!");
        }
    }

    public class TestEntity : Entity
    {
        public string Name { get; set; }
    }

}
