// See https://aka.ms/new-console-template for more information

using GoLive.Saturn.Data.Abstractions;
using GoLive.Saturn.Data.Entities;
using LiteDB;
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

var upsertTest = new ChildItem {Id= "686aecf600c04f09d28150c1"};
//var upsertTest = new ChildItem();

upsertTest.AdditionalData = "This is some additional data";

await repo.Upsert(upsertTest);


var par = await repo.One<ParentItem>(e => e.Id == "68082ee6ef897303ea42350f");

//ChildItem checkItem = await repo.One<ChildItem>(e => e.Scope == "68082ee6ef897303ea42350f");
ChildItem checkItem = await repo.One<ChildItem>(e => e.Scope == par);
var wibble = await ((IScopedRepository)repo).One<ChildItem, ParentItem>(par, e=>true);

ChildItem checkItem2 = await repo.One<ChildItem>(e => e.Scope == "68082ee6ef897303ea42350f");

ParentItem item = new ParentItem();
item.Data = "This is parent data";

await repo.Insert(item);

ChildItem childItem = new ChildItem();
childItem.Scope = item;
childItem.AdditionalData = $"This is child data as of {DateTime.Now:yyyy-MM-dd HH:mm:ss}";
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