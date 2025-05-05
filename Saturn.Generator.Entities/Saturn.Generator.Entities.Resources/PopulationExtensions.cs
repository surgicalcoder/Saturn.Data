using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using GoLive.Saturn.Data.Entities;

namespace GoLive.Saturn.Generator.Entities.Resources;

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


    public static async Task Populate<TMainItem, TShowItem>(this Ref<TMainItem> item, IList<TShowItem> items)
        where TMainItem : Entity, IUpdatableFrom<TShowItem>, ICreatableFrom<TShowItem>, new()
        where TShowItem : ICreatableFrom<TMainItem>, IUniquelyIdentifiable
    {
        if (item == null || string.IsNullOrWhiteSpace(item.Id))
        {
            return;
        }

        item.Item = TMainItem.Create(items.FirstOrDefault(e => e.Id == item.Id)) as TMainItem;
    }
}