using System;
using System.Collections.Generic;
using System.Linq.Expressions;
using System.Threading.Tasks;
using GoLive.Saturn.Data.Entities;

namespace GoLive.Saturn.Data.Abstractions
{
    public interface IScopedRepository : IScopedReadonlyRepository
    {
        Task Add<T, T2>(T2 scope, T entity, string overrideCollectionName = "")  where T : ScopedEntity<T2> where T2 : Entity, new();
        Task AddMany<T, T2>(IEnumerable<T> entities, string overrideCollectionName = "")  where T : ScopedEntity<T2> where T2 : Entity, new();
        Task AddMany<T, T2>(T2 scope, IEnumerable<T> entities, string overrideCollectionName = "")  where T : ScopedEntity<T2> where T2 : Entity, new();
        Task Add<T, T2>(string scope, T entity, string overrideCollectionName = "")  where T : ScopedEntity<T2> where T2 : Entity, new();
        Task AddMany<T, T2>(string scope, IEnumerable<T> entities, string overrideCollectionName = "")  where T : ScopedEntity<T2> where T2 : Entity, new();

        Task Update<T, T2>(T2 scope, T entity, string overrideCollectionName = "")  where T : ScopedEntity<T2> where T2 : Entity, new();
        Task UpdateMany<T, T2>(List<T> entity, string overrideCollectionName = "")  where T : ScopedEntity<T2> where T2 : Entity, new();
        Task UpdateMany<T, T2>(T2 scope, List<T> entity, string overrideCollectionName = "")  where T : ScopedEntity<T2> where T2 : Entity, new();
        Task Update<T, T2>(string scope, T entity, string overrideCollectionName = "")  where T : ScopedEntity<T2> where T2 : Entity, new();
        Task UpdateMany<T, T2>(string scope, List<T> entity, string overrideCollectionName = "")  where T : ScopedEntity<T2> where T2 : Entity, new();

        Task Upsert<T, T2>(T2 scope, T entity, string overrideCollectionName = "")  where T : ScopedEntity<T2> where T2 : Entity, new();
        Task UpsertMany<T, T2>(List<T> entity, string overrideCollectionName = "")  where T : ScopedEntity<T2> where T2 : Entity, new();
        Task UpsertMany<T, T2>(T2 scope, List<T> entity, string overrideCollectionName = "")  where T : ScopedEntity<T2> where T2 : Entity, new();
        
        Task Upsert<T, T2>(string scope, T entity, string overrideCollectionName = "")  where T : ScopedEntity<T2> where T2 : Entity, new();
        Task UpsertMany<T, T2>(string scope, List<T> entity, string overrideCollectionName = "")  where T : ScopedEntity<T2> where T2 : Entity, new();
        
        Task Delete<T, T2>(T2 scope, T entity, string overrideCollectionName = "")  where T : ScopedEntity<T2> where T2 : Entity, new();
        Task Delete<T, T2>(T2 scope, Expression<Func<T, bool>> filter, string overrideCollectionName = "")  where T : ScopedEntity<T2> where T2 : Entity, new();
        Task Delete<T, T2>(T2 scope, string Id, string overrideCollectionName = "")  where T : ScopedEntity<T2> where T2 : Entity, new();        
        Task Delete<T, T2>(string scope, T entity, string overrideCollectionName = "")  where T : ScopedEntity<T2> where T2 : Entity, new();
        Task Delete<T, T2>(string scope, string Id, string overrideCollectionName = "")  where T : ScopedEntity<T2> where T2 : Entity, new();
        
        Task DeleteMany<T, T2>(IEnumerable<T> entity, string overrideCollectionName = "")  where T : ScopedEntity<T2> where T2 : Entity, new();
        Task DeleteMany<T, T2>(T2 scope, IEnumerable<T> entity, string overrideCollectionName = "")  where T : ScopedEntity<T2> where T2 : Entity, new();
        Task DeleteMany<T, T2>(T2 scope, List<string> IDs, string overrideCollectionName = "")  where T : ScopedEntity<T2> where T2 : Entity, new();
        Task DeleteMany<T, T2>(string scope, IEnumerable<T> entity, string overrideCollectionName = "")  where T : ScopedEntity<T2> where T2 : Entity, new();
        Task DeleteMany<T, T2>(string scope, List<string> IDs, string overrideCollectionName = "")  where T : ScopedEntity<T2> where T2 : Entity, new();
    }
}