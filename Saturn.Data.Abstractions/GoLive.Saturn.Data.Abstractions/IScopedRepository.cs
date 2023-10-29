using System;
using System.Collections.Generic;
using System.Linq.Expressions;
using System.Threading.Tasks;
using GoLive.Saturn.Data.Entities;

namespace GoLive.Saturn.Data.Abstractions
{
    public interface IScopedRepository : IScopedReadonlyRepository
    {
        Task Insert<T, T2>(T2 scope, T entity)  where T : ScopedEntity<T2> where T2 : Entity, new();
        Task Insert<T, T2>(string scope, T entity)  where T : ScopedEntity<T2> where T2 : Entity, new();
        /*Task InsertMany<T, T2>(IEnumerable<T> entities)  where T : ScopedEntity<T2> where T2 : Entity, new();*/
        Task InsertMany<T, T2>(T2 scope, IEnumerable<T> entities)  where T : ScopedEntity<T2> where T2 : Entity, new();
        Task InsertMany<T, T2>(string scope, IEnumerable<T> entities)  where T : ScopedEntity<T2> where T2 : Entity, new();

        Task Update<T, T2>(T2 scope, T entity)  where T : ScopedEntity<T2> where T2 : Entity, new();
        Task Update<T, T2>(string scope, T entity)  where T : ScopedEntity<T2> where T2 : Entity, new();
        /*Task UpdateMany<T, T2>(List<T> entity)  where T : ScopedEntity<T2> where T2 : Entity, new();*/
        Task UpdateMany<T, T2>(T2 scope, List<T> entity)  where T : ScopedEntity<T2> where T2 : Entity, new();
        Task UpdateMany<T, T2>(string scope, List<T> entity)  where T : ScopedEntity<T2> where T2 : Entity, new();
        
        Task JsonUpdate<T, T2>(string scope, string id, int version, string json) where T : ScopedEntity<T2> where T2 : Entity, new();
        
        Task Upsert<T, T2>(T2 scope, T entity)  where T : ScopedEntity<T2> where T2 : Entity, new();
        Task Upsert<T, T2>(string scope, T entity)  where T : ScopedEntity<T2> where T2 : Entity, new();
        
        Task UpsertMany<T, T2>(List<T> entity)  where T : ScopedEntity<T2> where T2 : Entity, new();
        Task UpsertMany<T, T2>(T2 scope, List<T> entity)  where T : ScopedEntity<T2> where T2 : Entity, new();
        Task UpsertMany<T, T2>(string scope, List<T> entity)  where T : ScopedEntity<T2> where T2 : Entity, new();
        
        Task Delete<T, T2>(T2 scope, T entity)  where T : ScopedEntity<T2> where T2 : Entity, new();
        Task Delete<T, T2>(T2 scope, Expression<Func<T, bool>> filter)  where T : ScopedEntity<T2> where T2 : Entity, new();
        Task Delete<T, T2>(T2 scope, string id)  where T : ScopedEntity<T2> where T2 : Entity, new();        
        Task Delete<T, T2>(string scope, T entity)  where T : ScopedEntity<T2> where T2 : Entity, new();
        Task Delete<T, T2>(string scope, string id)  where T : ScopedEntity<T2> where T2 : Entity, new();
        Task Delete<T, T2>(string scope, Expression<Func<T, bool>> filter)  where T : ScopedEntity<T2> where T2 : Entity, new();
        
        /*Task DeleteMany<T, T2>(IEnumerable<T> entity)  where T : ScopedEntity<T2> where T2 : Entity, new();*/
        Task DeleteMany<T, T2>(T2 scope, IEnumerable<T> entities)  where T : ScopedEntity<T2> where T2 : Entity, new();
        Task DeleteMany<T, T2>(T2 scope, List<string> IDs)  where T : ScopedEntity<T2> where T2 : Entity, new();
        Task DeleteMany<T, T2>(string scope, IEnumerable<T> entity)  where T : ScopedEntity<T2> where T2 : Entity, new();
        Task DeleteMany<T, T2>(string scope, List<string> ds)  where T : ScopedEntity<T2> where T2 : Entity, new();
    }
}