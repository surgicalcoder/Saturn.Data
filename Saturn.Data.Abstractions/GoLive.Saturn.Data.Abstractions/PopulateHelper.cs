using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Threading.Tasks;
using GoLive.Saturn.Data.Entities;

namespace GoLive.Saturn.Data.Abstractions
{
    public static class PopulateHelper
    {
        public static async Task Populate<T>(this Ref<T> item, IReadonlyRepository repository) where T : Entity, new()
        {
            item.Item = await repository.ByRef(item);
        }

        public static void Populate<T>(this Ref<T> item, List<T> Items) where T : Entity, new()
        {
            item.Item = Items.FirstOrDefault(f => f.Id == item.Id);
        }

        public static async Task Populate<T>(this List<Ref<T>> item, IReadonlyRepository repository) where T : Entity, new()
        {
            var IDs = item.Select(f => f.Id);
            var items = (await repository.Many<T>(f => IDs.Contains(f.Id))).ToList();
            item.ForEach(f => f.Fetch(items));

        }

#pragma warning disable 1998
        public static async Task Populate<T>(this List<Ref<T>> item, List<T> items) where T : Entity, new()
#pragma warning restore 1998
        {
            item.ForEach(f => f.Fetch(items));
        }

        public static async Task<List<T>> Populate<T, T2>(this List<T> collection, Expression<Func<T, Ref<T2>>> item, IReadonlyRepository repository) where T2 : Entity, new()
        {
            var compile = item.Compile();

            var IDs = collection.Where(f=> compile.Invoke(f) != null).Select(f => compile.Invoke(f)).Select(r => r.Id).ToList();

            var items = (await repository.Many<T2>(f => IDs.Contains(f.Id))).ToList();

            collection.ForEach(delegate (T obj)
            {
                var re = compile.Invoke(obj);

                if (re != null)
                {
                    re.Fetch(items);
                }
            });

            return collection;
        }

#pragma warning disable 1998
        public static async Task<List<T>> Populate<T, T2>(this List<T> collection, Expression<Func<T, Ref<T2>>> item, List<T2> items) where T2 : Entity, new()
#pragma warning restore 1998
        {
            var compile = item.Compile();

            collection.ForEach(delegate (T obj)
            {
                var re = compile.Invoke(obj);

                if (re != null)
                {
                    re.Fetch(items);
                }
            });

            return collection;
        }

        public static async Task<List<T>> PopulateMultiple<T, T2>(this List<T> collection, Expression<Func<T, List<Ref<T2>>>> item, IReadonlyRepository repository) where T2 : Entity, new()
        {
            var compile = item.Compile();

            var IDs = collection.SelectMany(f => compile.Invoke(f)).Select(r => r.Id).ToList();

            var items = (await repository.Many<T2>(f => IDs.Contains(f.Id))).ToList();

            collection.ForEach(delegate (T obj)
            {
                compile.Invoke(obj).ForEach(delegate (Ref<T2> r2)
                {
                    r2.Fetch(items);
                });
            });

            return collection;
        }

#pragma warning disable 1998
        public static async Task<List<T>> PopulateMultiple<T, T2>(this List<T> collection, Expression<Func<T, List<Ref<T2>>>> item, List<T2> items) where T2 : Entity, new()
#pragma warning restore 1998
        {
            var compile = item.Compile();

            collection.ForEach(delegate (T obj)
            {
                compile.Invoke(obj).ForEach(delegate (Ref<T2> r2)
                {
                    r2.Fetch(items);
                });
            });

            return collection;
        }

    }
}