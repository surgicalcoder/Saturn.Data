using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using GoLive.Saturn.Data.Entities;

namespace GoLive.Saturn.Generator.Entities.Resources;

public static class PopulationExtensions
{
    public static Task Populate<TItem, TShowItem>(this IList<Ref<TItem>> item, IList<TShowItem> items)
        where TItem : Entity, IUpdatableFrom<TShowItem>, new()
        where TShowItem : ICreatableFrom<TItem>, IUniquelyIdentifiable
    {
        if (item == null || item.Count == 0)
        {
            return Task.CompletedTask;
        }

        foreach (var f in item)
        {
            f.Item ??= new TItem();

            using (f.Item is ISuppressTracking suppress ? suppress.SuppressTracking() : null)
            {
                f.Item.UpdateFrom(items.FirstOrDefault(e => e.Id == f.Id));
            }

            f.Item.Id = f.Id;
        }

        return Task.CompletedTask;
    }

    public static Task Populate<TItem, TShowItem>(this IList<TItem> item, IList<TShowItem> items)
        where TItem : Entity, IUpdatableFrom<TShowItem>, new()
        where TShowItem : ICreatableFrom<TItem>, IUniquelyIdentifiable
    {
        if (item == null || item.Count == 0)
        {
            return Task.CompletedTask;
        }

        foreach (var f in item)
        {
            using (f is ISuppressTracking suppress ? suppress.SuppressTracking() : null)
            {
                f.UpdateFrom(items.FirstOrDefault(e => e.Id == f.Id));
            }

            f.Id = f.Id;
        }

        return Task.CompletedTask;
    }

    public static Task Populate<TMainItem, TShowItem>(this Ref<TMainItem> item, IList<TShowItem> items)
        where TMainItem : Entity, IUpdatableFrom<TShowItem>, ICreatableFrom<TShowItem>, new()
        where TShowItem : ICreatableFrom<TMainItem>, IUniquelyIdentifiable
    {
        if (item == null || string.IsNullOrWhiteSpace(item.Id))
        {
            return Task.CompletedTask;
        }

        item.Item = TMainItem.Create(items.FirstOrDefault(e => e.Id == item.Id)) as TMainItem;

        return Task.CompletedTask;
    }

    public static Task Populate<TItem, TShowItem>(this IList<Ref<TItem>> item, IList<TShowItem> items, Func<TShowItem, string> keySelector)
        where TItem : Entity, IUpdatableFrom<TShowItem>, new()
        where TShowItem : ICreatableFrom<TItem>
    {
        if (item == null || item.Count == 0)
        {
            return Task.CompletedTask;
        }

        foreach (var f in item)
        {
            f.Item ??= new TItem();

            using (f.Item is ISuppressTracking suppress ? suppress.SuppressTracking() : null)
            {
                f.Item.UpdateFrom(items.FirstOrDefault(e => keySelector(e) == f.Id));
            }

            f.Item.Id = f.Id;
        }

        return Task.CompletedTask;
    }

    public static Task Populate<TItem, TShowItem>(this IList<TItem> item, IList<TShowItem> items, Func<TShowItem, string> keySelector)
        where TItem : Entity, IUpdatableFrom<TShowItem>, new()
        where TShowItem : ICreatableFrom<TItem>
    {
        if (item == null || item.Count == 0)
        {
            return Task.CompletedTask;
        }

        foreach (var f in item)
        {
            using (f is ISuppressTracking suppress ? suppress.SuppressTracking() : null)
            {
                f.UpdateFrom(items.FirstOrDefault(e => keySelector(e) == f.Id));
            }
        }

        return Task.CompletedTask;
    }
}
