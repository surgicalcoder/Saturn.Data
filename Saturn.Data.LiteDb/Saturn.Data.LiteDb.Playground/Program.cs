// See https://aka.ms/new-console-template for more information

using GoLive.Saturn.Data.Abstractions;
using GoLive.Saturn.Data.Entities;
using Saturn.Data.LiteDb;

LiteDBRepository repo = new LiteDBRepository(new RepositoryOptions()
{
    ConnectionString = "e:\\_scratch\\litedb-playground.db"
}, new LiteDBRepositoryOptions());


TestEntity1 ent1 = new TestEntity1();
ent1.Val1 = Guid.NewGuid().ToString("N");

await repo.Insert(ent1);

public class TestEntity1 : Entity
{
    public string Val1 { get; set; }
}