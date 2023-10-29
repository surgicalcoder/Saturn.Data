using System;
using System.Collections.Generic;
using System.Linq.Expressions;
using System.Threading.Tasks;
using GoLive.Saturn.Data.Entities;

namespace GoLive.Saturn.Data.Abstractions
{
    public interface IRepository : IReadonlyRepository
    {
        Task Insert<T>(T entity) where T : Entity;
        Task InsertMany<T>(IEnumerable<T> entities) where T : Entity;
        
        Task Save<T>(T entity) where T : Entity;
        Task SaveMany<T>(T entity) where T : Entity;
        
        Task Update<T>(T entity) where T : Entity;
        Task UpdateMany<T>(List<T> entities) where T : Entity;

        Task Upsert<T>(T entity) where T : Entity;
        Task UpsertMany<T>(List<T> entity) where T : Entity;
        
        Task Delete<T>(T entity) where T : Entity;
        Task Delete<T>(Expression<Func<T, bool>> filter) where T : Entity;
        Task Delete<T>(string id) where T : Entity;
        
        Task DeleteMany<T>(IEnumerable<T> entities) where T : Entity;
        Task DeleteMany<T>(List<string> IDs) where T : Entity;
        
        Task JsonUpdate<T>(string id, int version, string json) where T : Entity;
        void InitDatabase();
    }
}