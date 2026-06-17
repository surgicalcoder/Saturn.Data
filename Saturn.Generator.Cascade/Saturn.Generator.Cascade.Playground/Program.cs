using GoLive.Saturn.Data.Entities.Cascade;

namespace Saturn.Generator.Cascade.Playground;

public static class Program
{
    public static void Main()
    {
        Console.WriteLine("Account: " + Account.__Cascade.For.Length);
        Console.WriteLine("Post: " + Post.__Cascade.For.Length);
        Console.WriteLine("Tag: " + Tag.__Cascade.For.Length);
        foreach (var r in Account.__Cascade.For) Console.WriteLine($"  Account: {r.ParentType.Name} -> {r.ChildType.Name} mode={r.Mode}");
        foreach (var r in Post.__Cascade.For) Console.WriteLine($"  Post: {r.ParentType.Name} -> {r.ChildType.Name} mode={r.Mode}");
        foreach (var r in Tag.__Cascade.For) Console.WriteLine($"  Tag: {r.ParentType.Name} -> {r.ChildType.Name} mode={r.Mode}");
    }
}
