using System;
using System.Collections.Generic;
using GoLive.Saturn.Data.Entities;
using System.Threading.Tasks;
using System.Linq;

namespace GoLive.Generator.Saturn.Resources;

public static class DictionaryExt
{
    public static void Upsert<TKey, TValue>(this System.Collections.Generic.IDictionary<TKey, TValue> dictionary, TKey key, TValue value)
    {
        if (dictionary.ContainsKey(key))
        {
            dictionary[key] = value;
        }
        else
        {
            dictionary.Add(key, value);
        }
    }
}

[AttributeUsage(AttributeTargets.Field, AllowMultiple = false, Inherited = false)]
public class ReadonlyAttribute : Attribute { }

[AttributeUsage(AttributeTargets.Field, AllowMultiple = false, Inherited = false)]
public class WriteOnlyAttribute : Attribute { }


[AttributeUsage(AttributeTargets.Field | AttributeTargets.Property, AllowMultiple = false, Inherited = false)]
public class DoNotTrackChangesAttribute : Attribute { }


[AttributeUsage(AttributeTargets.Field | AttributeTargets.Property, AllowMultiple = false, Inherited = false)]
public class AddRefToScopeAttribute : Attribute { }


[AttributeUsage(AttributeTargets.Field | AttributeTargets.Property, AllowMultiple = true, Inherited = false)]
public class AddToLimitedViewAttribute : Attribute
{
    public AddToLimitedViewAttribute(string ViewName, bool TwoWay = false)
    {
        this.ViewName = ViewName;
        this.TwoWay = TwoWay;
    }
    public string ViewName { get; set; }
    public string UseLimitedView { get; set; }
    public bool TwoWay { get; set; }
}


[AttributeUsage(AttributeTargets.Class, AllowMultiple = true, Inherited = true)]
public class AddParentItemToLimitedViewAttribute : Attribute
{
    public AddParentItemToLimitedViewAttribute(string ViewName, string ParentField)
    {
        this.ViewName = ViewName;
        this.ParentField = ParentField;
    }
    internal string ViewName { get; set; }
    internal string ParentField { get; set; }
    public string ChildField { get; set; }
    public string UseLimitedView { get; set; }
    public bool TwoWay { get; set; }
    public bool InheritFromIUniquelyIdentifiable { get; set; }
}

public interface ICreatableFrom<T>
{
    static abstract ICreatableFrom<T> Create(T input);
}


public static class PopulationExtensions
{
    public static async Task Populate<TItem, TShowItem>(this IList<Ref<TItem>> item, IList<TShowItem> items) 
        where TItem : Entity, IUpdatableFrom<TShowItem>, new() 
        where TShowItem : ICreatableFrom<TItem>, IUniquelyIdentifiable
    {
        if (item == null || item.Count == 0)
        {
            return;
        }
        
        foreach (var f in item)
        {
            f.Item ??= new TItem();
            f.Item.UpdateFrom(items.FirstOrDefault(e => e.Id == f.Id));
            f.Item.Id = f.Id;
        }
    }    
    
    public static async Task Populate<TItem, TShowItem>(this IList<TItem> item, IList<TShowItem> items) 
        where TItem : Entity, IUpdatableFrom<TShowItem>, new() 
        where TShowItem : ICreatableFrom<TItem>, IUniquelyIdentifiable
    {
        if (item == null || item.Count == 0)
        {
            return;
        }
        
        foreach (var f in item)
        {
            f.UpdateFrom(items.FirstOrDefault(e => e.Id == f.Id));
            f.Id = f.Id;
        }
    }
}