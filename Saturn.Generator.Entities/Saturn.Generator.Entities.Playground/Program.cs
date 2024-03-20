// See https://aka.ms/new-console-template for more information

using GoLive.Saturn.Data;
using GoLive.Saturn.Data.Abstractions;
using Saturn.Generator.Entities.Playground;

FourthItem fr = new();
//var wibble = FourthItem_View1.Generate(fr);

/*MainItem item = new MainItem();
item.Strings = new ObservableList<string>();
item.AnotherString = new ObservableList<string>();
item.Strings.CollectionChanged += (in NotifyCollectionChangedEventArgs<string> eventArgs) => item.Changes.Upsert($"Strings.{eventArgs.NewStartingIndex}", eventArgs.NewItems);

item.Name = "init name value";
item.Description = "init desc value";
item.AnotherString = new();
item.AnotherString.Add("Wibble 1");
item.Id = DateTime.UtcNow.ToString("O");
item.EnableChangeTracking = true;
item.Changes.Clear();


var fi = new FourthItem();
fi.EnableChangeTracking = true;
fi.MainItem = item;
item.Strings.Add("strings 1");
fi.MainItem = null;


item.Name = "Updated Name";
item.AnotherString.Add("Wibble 2");
item.AnotherString.Insert(0, "Wibble 3");
item.Strings.Add("string 2");
item.Strings[0] = "overwritten";
Console.WriteLine();*/

RepositoryOptions options = new()
{
ConnectionString = "mongodb://localhost/ReferenceTests",
GetCollectionName = type => type.Name
};

var repo = new Repository(options);

var scope = new ReferenceTestScope();
scope.Name = $"Test scope created at {DateTime.UtcNow:f}";
await repo.Insert(scope);

ReferenceTest2 ref2 = new() { TestName = $"Test item2 created at {DateTime.UtcNow:f}" };
await repo.Insert(ref2);

ReferenceTest3 ref3 =new() { AnotherName = $"Test item2 created at {DateTime.UtcNow:f}" };
await repo.Insert(ref3);

ReferenceTest1 ref1 = new ReferenceTest1();
ref1.Test2 = ref2;
ref1.Test3 = ref3;
ref1.Scope = scope;
await repo.Insert(ref1);

/*
public class t2
{
    private string blarg;
    public string Blarg
    {
        get => blarg;
        set
        {
            SetField(ref blarg, value);
        }
    }
}*/