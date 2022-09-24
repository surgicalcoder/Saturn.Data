using System;
using System.Collections.Generic;
using System.Linq.Expressions;
using System.Threading.Tasks;
using GoLive.Saturn.Data.Entities;

namespace GoLive.Saturn.Data.Abstractions
{
    public interface IRepository : IReadonlyRepository
    {
        Task Add<T>(T entity, string overrideCollectionName = "") where T : Entity;
        Task AddMany<T>(IEnumerable<T> entities, string overrideCollectionName = "") where T : Entity;
        
        Task Update<T>(T entity, string overrideCollectionName = "") where T : Entity;
        Task UpdateMany<T>(List<T> entity, string overrideCollectionName = "") where T : Entity;

        Task Upsert<T>(T entity, string overrideCollectionName = "") where T : Entity;
        Task UpsertMany<T>(List<T> entity, string overrideCollectionName = "") where T : Entity;
        
        Task Delete<T>(T entity, string overrideCollectionName = "") where T : Entity;
        Task Delete<T>(Expression<Func<T, bool>> filter, string overrideCollectionName = "") where T : Entity;
        Task Delete<T>(string Id, string overrideCollectionName = "") where T : Entity;
        Task DeleteMany<T>(IEnumerable<T> entity, string overrideCollectionName = "") where T : Entity;
        Task DeleteMany<T>(List<string> IDs, string overrideCollectionName = "") where T : Entity;
        
        void InitDatabase();
    }
}