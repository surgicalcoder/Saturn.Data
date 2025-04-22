// See https://aka.ms/new-console-template for more information

using GoLive.Saturn.Data.Abstractions;
using GoLive.Saturn.Data.Entities;
using Saturn.Data.LiteDb;

LiteDBRepository repo = new LiteDBRepository(new RepositoryOptions()
{
    ConnectionString = "e:\\_scratch\\litedb-playground.db"
}, new LiteDBRepositoryOptions());


/*
TestEntity1 ent1 = new TestEntity1();
ent1.Val1 = Guid.NewGuid().ToString("N");

await repo.Insert(ent1);
*/

//ChildItem item = await repo.ById<ChildItem>("680824a49a8a1a0e8adba3a5");


ParentItem item = new ParentItem();
item.Data = "This is parent data";

await repo.Insert(item);

ChildItem childItem = new ChildItem();
childItem.Scope = item;
childItem.AdditionalData = "This is child data";
await repo.Insert(childItem);

Console.WriteLine("Done...");


public class TestEntity1 : Entity
{
    public string Val1 { get; set; }
}


public class ParentItem : Entity
{
    public string Data { get; set; }
}

public class ChildItem : ScopedEntity<ParentItem>
{
    public string AdditionalData { get; set; }
}