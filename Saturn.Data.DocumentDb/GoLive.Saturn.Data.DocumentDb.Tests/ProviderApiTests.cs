using Shiny.DocumentDb;
using Shiny.DocumentDb.Sqlite;
using Xunit.Abstractions;

namespace Saturn.Data.DocumentDb.Tests;

public class ProviderApiTests(ITestOutputHelper output)
{
    private sealed class Probe
    {
        public string Id { get; set; } = "";

        public string? Name { get; set; }

        public int Count { get; set; }
    }

    private static DocumentStore CreateStore()
    {
        var path = Path.Combine(Path.GetTempPath(), $"saturn-docdb-probe-{Guid.NewGuid():N}.db");

        return new DocumentStore(new DocumentStoreOptions
        {
            DatabaseProvider = new SqliteDatabaseProvider($"Data Source={path}")
        });
    }

    private const string HexId = "68bdd5525324ff2610c4361d";

    [Fact]
    public async Task Insert_With_Explicit_24Hex_Id_Writes_It_Back_Unchanged()
    {
        using var store = CreateStore();
        var probe = new Probe { Id = HexId, Name = "a" };

        await store.Insert(probe);

        Assert.Equal(HexId, probe.Id);
        var fetched = await store.Get<Probe>(HexId);
        Assert.NotNull(fetched);
        Assert.Equal(HexId, fetched!.Id);
    }

    [Fact]
    public async Task Update_And_Upsert_Roundtrip()
    {
        using var store = CreateStore();
        await store.Insert(new Probe { Id = HexId, Name = "a", Count = 1 });

        await store.Update(new Probe { Id = HexId, Name = "b", Count = 2 });
        var updated = await store.Get<Probe>(HexId);
        Assert.Equal("b", updated!.Name);

        await store.Upsert(new Probe { Id = HexId, Name = "c" });
        var merged = await store.Get<Probe>(HexId);
        Assert.Equal("c", merged!.Name);
    }

    [Fact]
    public async Task Remove_Returns_True_Then_False()
    {
        using var store = CreateStore();
        await store.Insert(new Probe { Id = HexId, Name = "a" });

        Assert.True(await store.Remove<Probe>(HexId));
        Assert.False(await store.Remove<Probe>(HexId));
    }

    [Fact]
    public async Task Batch_Insert_And_Remove()
    {
        using var store = CreateStore();
        var items = Enumerable.Range(0, 20)
            .Select(index => new Probe { Id = $"pad{index:D21}", Name = $"n{index}" })
            .ToList();

        var inserted = await store.BatchInsert(items);
        Assert.Equal(20, inserted);

        var removed = await store.BatchRemove<Probe>(items.Take(5).Select(item => item.Id).ToList());
        Assert.Equal(5, removed);
    }

    [Fact]
    public async Task Query_Where_OrderBy_ToList_Count()
    {
        using var store = CreateStore();
        await store.BatchInsert(Enumerable.Range(0, 30)
            .Select(index => new Probe { Id = $"pad{index:D21}", Name = $"n{index:D2}", Count = index })
            .ToList());

        var matches = await store.Query<Probe>().Where(item => item.Count >= 10).ToList();
        Assert.Equal(20, matches.Count);

        var ordered = await store.Query<Probe>().OrderBy(item => item.Count).ToList();
        Assert.Equal(0, ordered[0].Count);

        var count = await store.Query<Probe>().Where(item => item.Count >= 10).Count();
        Assert.Equal(20, count);
    }

    [Fact]
    public async Task Id_Range_Query_Is_Ordered()
    {
        using var store = CreateStore();
        await store.BatchInsert(Enumerable.Range(0, 30)
            .Select(index => new Probe { Id = $"pad{index:D21}", Name = $"n{index:D2}" })
            .ToList());

        var ids = new List<string> { "pad000000000000000000016", "pad000000000000000000017", "pad000000000000000000018" };
        var after = await store.Query<Probe>().Where(item => ids.Contains(item.Id)).OrderBy(item => item.Id).ToList();

        Assert.Equal(3, after.Count);
        Assert.Equal("pad000000000000000000016", after[0].Id);
    }

    [Fact]
    public async Task SetProperty_And_RemoveProperty()
    {
        using var store = CreateStore();
        await store.Insert(new Probe { Id = HexId, Name = "a" });

        await store.SetProperty<Probe>(HexId, item => item.Name, "b");
        var set = await store.Get<Probe>(HexId);
        Assert.Equal("b", set!.Name);

        await store.RemoveProperty<Probe>(HexId, item => item.Name);
        var removed = await store.Get<Probe>(HexId);
        Assert.Null(removed!.Name);
    }

    [Fact]
    public async Task OpenSession_Commits()
    {
        using var store = CreateStore();

        await using (var session = store.OpenSession())
        {
            session.Add(new Probe { Id = HexId, Name = "committed" });
            await session.SaveChanges();
        }

        Assert.NotNull(await store.Get<Probe>(HexId));
    }

    [Fact]
    public async Task OpenSession_BeginTransaction_Then_Discard()
    {
        using var store = CreateStore();
        var rolledBackId = "pad000000000000000000099";

        try
        {
            await using var session = store.OpenSession();
            await session.BeginTransaction();
            session.Add(new Probe { Id = rolledBackId, Name = "rolled-back" });
            await session.SaveChanges();
        }
        catch (Exception exception)
        {
            output.WriteLine($"Explicit transaction probe exception: {exception.GetType().Name}: {exception.Message}");
        }

        var visibleInside = false;

        await using (var session = store.OpenSession())
        {
            var read = await store.Get<Probe>(rolledBackId);
            visibleInside = read is not null;
        }

        output.WriteLine($"Uncommitted/buffered write visible after SaveChanges in explicit transaction: {visibleInside}");
    }

    [Fact]
    public void Capability_Properties_Read()
    {
        using var store = CreateStore();

        output.WriteLine($"SupportsTransactions={store.SupportsTransactions}");
        output.WriteLine($"SupportsPessimisticLocking={store.SupportsPessimisticLocking}");
        output.WriteLine($"SupportsSpatial={store.SupportsSpatial}");
        output.WriteLine($"SupportsVector={store.SupportsVector}");
        output.WriteLine($"SupportsFullText={store.SupportsFullText}");
        output.WriteLine($"MaxBlobSize={store.MaxBlobSize}");
    }

    [Fact]
    public void Provider_Capability_Properties_Read()
    {
        IDatabaseProvider provider = new SqliteDatabaseProvider("Data Source=:memory:");

        output.WriteLine($"RequiresSingleConnection={provider.RequiresSingleConnection}");
        output.WriteLine($"SupportsUniqueIndexes={provider.SupportsUniqueIndexes}");
        output.WriteLine($"SupportsJsonMergePatch={provider.SupportsJsonMergePatch}");
        output.WriteLine($"SupportsBatchUpsert={provider.SupportsBatchUpsert}");
        output.WriteLine($"SupportsChangeFeed={provider.SupportsChangeFeed}");
        output.WriteLine($"SupportsTemporal={provider.SupportsTemporal}");
    }
}
