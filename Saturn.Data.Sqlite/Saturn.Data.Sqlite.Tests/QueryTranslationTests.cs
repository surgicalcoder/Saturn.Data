using System.Linq.Expressions;
using Saturn.Data.Sqlite.Query;
using Saturn.Data.Testing.Shared.ChangeFeed;
using Saturn.Data.Testing.Shared.Entities;

namespace Saturn.Data.Sqlite.Tests;

public class QueryTranslationTests
{
    [Fact]
    public void True_Constant()
    {
        Expression<Func<BasicEntity, bool>> predicate = entity => true;
        Assert.Equal("1=1", SqliteRepository.TranslatePredicateSql(predicate));
    }

    [Fact]
    public void Id_Equality_Uses_Column()
    {
        var id = "000000000000000000000001";
        Expression<Func<BasicEntity, bool>> predicate = entity => entity.Id == id;
        Assert.Equal("_id = @p0", SqliteRepository.TranslatePredicateSql(predicate));
    }

    [Fact]
    public void Property_Equality_Uses_JsonExtract()
    {
        Expression<Func<BasicEntity, bool>> predicate = entity => entity.Name == "abc";
        Assert.Equal("json_extract(_doc, '$.Name') = @p0", SqliteRepository.TranslatePredicateSql(predicate));
    }

    [Fact]
    public void String_Contains_Uses_Instr()
    {
        Expression<Func<BasicEntity, bool>> predicate = entity => entity.Name.Contains("abc");
        Assert.Equal("instr(json_extract(_doc, '$.Name'), @p0) > 0", SqliteRepository.TranslatePredicateSql(predicate));
    }

    [Fact]
    public void String_StartsWith_Uses_Like()
    {
        Expression<Func<BasicEntity, bool>> predicate = entity => entity.Name.StartsWith("abc");
        var sql = SqliteRepository.TranslatePredicateSql(predicate);
        Assert.Contains("LIKE", sql);
        Assert.Contains("json_extract(_doc, '$.Name')", sql);
    }

    [Fact]
    public void Id_List_Contains_Uses_In()
    {
        var ids = new[] { "000000000000000000000001", "000000000000000000000002" };
        Expression<Func<BasicEntity, bool>> predicate = entity => ids.Contains(entity.Id);
        Assert.Equal("_id IN (@p0, @p1)", SqliteRepository.TranslatePredicateSql(predicate));
    }

    [Fact]
    public void Greater_Than_Uses_JsonExtract()
    {
        Expression<Func<ChangeFeedEntity, bool>> predicate = entity => entity.Count > 3;
        Assert.Equal("json_extract(_doc, '$.Count') > @p0", SqliteRepository.TranslatePredicateSql(predicate));
    }

    [Fact]
    public void Reference_Equality_Uses_Scope_Path()
    {
        var scope = "68bdd5525324ff2610c4361d";
        Expression<Func<ChildEntity, bool>> predicate = entity => entity.Scope == scope;
        Assert.Equal("json_extract(_doc, '$.Scope') = @p0", SqliteRepository.TranslatePredicateSql(predicate));
    }

    [Fact]
    public void Unsupported_Call_Is_Detected()
    {
        Expression<Func<BasicEntity, bool>> predicate = entity => entity.Equals(new BasicEntity());
        Assert.Throws<SqliteTranslationException>(() => SqliteRepository.TranslatePredicateSql(predicate));
    }
}
