using System.Linq.Expressions;
using GoLive.Saturn.Data.Abstractions;
using GoLive.Saturn.Data.Entities;
using LiteDB.Queryable;

namespace Saturn.Data.LiteDb;

public partial class Repository : IScopedRepository
{
    public async Task<T> ById<T, T2>(T2 scope, string id) where T : ScopedEntity<T2> where T2 : Entity, new()
    {
        var result = await getCollection<T>().FindOneAsync(e => e.Id == id && e.Scope == scope);

        return result;
    }

        public async Task<List<T>> ById<T, T2>(T2 scope, List<string> IDs) where T : ScopedEntity<T2> where T2 : Entity, new()
        {
            var result = await getCollection<T>().FindAsync(e => IDs.Contains(e.Id) && e.Scope == scope);
            
            return result.ToList();
        }

        public async Task<IQueryable<T>> All<T, T2>(T2 scope) where T : ScopedEntity<T2> where T2 : Entity, new()
        {
            var scopedEntities = getCollection<T>().AsQueryable().Where(f => f.Scope == scope);
            return await Task.Run(() => scopedEntities);
        }

        public async Task<T> One<T, T2>(T2 scope, Expression<Func<T, bool>> predicate) where T : ScopedEntity<T2> where T2 : Entity, new()
        {
            Expression<Func<T, bool>> firstPred = item => item.Scope == scope;
            var combinedPred = firstPred.And(predicate);
            var result = await getCollection<T>().FindOneAsync(combinedPred);

            return result;
        }

        public async Task<IQueryable<T>> Many<T, T2>(T2 scope, Expression<Func<T, bool>> predicate) where T : ScopedEntity<T2> where T2 : Entity, new()
        {
            var scopedEntities = getCollection<T>().AsQueryable().Where(f => f.Scope == scope).Where(predicate);

            return await Task.Run(() => scopedEntities);
        }

        public async Task<IQueryable<T>> Many<T, T2>(T2 scope, Expression<Func<T, bool>> predicate, int pageSize, int PageNumber) where T : ScopedEntity<T2> where T2 : Entity, new()
        {
            if (pageSize == 0 || PageNumber == 0)
            {
                return await Many(scope, predicate).ConfigureAwait(false);
            }

            var res = getCollection<T>().AsQueryable().Where(f => f.Scope == scope).Where(predicate).Skip((PageNumber - 1) * pageSize).Take(pageSize);
            return await Task.Run(() => res);
        }

        public async Task<long> CountMany<T, T2>(T2 scope, Expression<Func<T, bool>> predicate) where T : ScopedEntity<T2> where T2 : Entity, new()
        {
           Expression< Func<T, bool>>firstPred = item => item.Scope == scope;
           var combinedPred = firstPred.And(predicate);
            
            return await getCollection<T>().CountAsync(combinedPred);
        }


        public async Task Add<T, T2>(T2 scope, T entity, string overrideCollectionName = "") where T : ScopedEntity<T2> where T2 : Entity, new()
        {
            entity.Scope = scope;
            await Add(entity);
        }

        public async Task AddMany<T, T2>(IEnumerable<T> entities, string overrideCollectionName = "") where T : ScopedEntity<T2> where T2 : Entity, new()
        {
            await AddMany(entities);
        }

        public async Task AddMany<T, T2>(T2 scope, IEnumerable<T> entities, string overrideCollectionName = "") where T : ScopedEntity<T2> where T2 : Entity, new()
        {
            foreach (var scopedEntity in entities)
            {
                scopedEntity.Scope = scope;
            }
            
            await AddMany(entities);
        }

        public async Task Update<T, T2>(T2 scope, T entity, string overrideCollectionName = "") where T : ScopedEntity<T2> where T2 : Entity, new()
        {
            entity.Scope = scope;
            await Update(entity);
        }

        public async Task UpdateMany<T, T2>(List<T> entity, string overrideCollectionName = "") where T : ScopedEntity<T2> where T2 : Entity, new()
        {
            await UpdateMany(entity);
        }

        public async Task UpdateMany<T, T2>(T2 scope, List<T> entity, string overrideCollectionName = "") where T : ScopedEntity<T2> where T2 : Entity, new()
        {
            entity.ForEach(f=>f.Scope = scope);
            await UpdateMany(entity);
        }


        public async Task JsonUpdate<T, T2>(string Scope, string Id, int Version, string Json, string overrideCollectionName = "") where T : ScopedEntity<T2> where T2 : Entity, new()
        {
            throw new PlatformNotSupportedException();
        }

        public async Task Upsert<T, T2>(T2 scope, T entity, string overrideCollectionName = "") where T : ScopedEntity<T2> where T2 : Entity, new()
        {
            entity.Scope = scope;
            await Upsert(entity);
        }

        public async Task UpsertMany<T, T2>(List<T> entity, string overrideCollectionName = "") where T : ScopedEntity<T2> where T2 : Entity, new()
        {
            await UpsertMany(entity);
        }

        public async Task UpsertMany<T, T2>(T2 scope, List<T> entity, string overrideCollectionName = "") where T : ScopedEntity<T2> where T2 : Entity, new()
        {
            entity.ForEach(f=>f.Scope = scope);
            await UpsertMany(entity);
        }

        public async Task Delete<T, T2>(T2 scope, T entity, string overrideCollectionName = "") where T : ScopedEntity<T2> where T2 : Entity, new()
        {
            await Delete<T>(f => f.Scope == scope && f.Id == entity.Id);
        }

        public async Task Delete<T, T2>(T2 scope, Expression<Func<T, bool>> filter, string overrideCollectionName = "") where T : ScopedEntity<T2> where T2 : Entity, new()
        {
            await Delete<T>(filter.And(e => e.Scope == scope));
        }

        public async Task Delete<T, T2>(T2 scope, string Id, string overrideCollectionName = "") where T : ScopedEntity<T2> where T2 : Entity, new()
        {
            await Delete<T>(f => f.Scope == scope && f.Id == Id);
        }

        public async Task DeleteMany<T, T2>(IEnumerable<T> entity, string overrideCollectionName = "") where T : ScopedEntity<T2> where T2 : Entity, new()
        {
            var items = entity.Where(f=>f.Scope != null).ToList();
            await DeleteMany(items); 
        }

        public async Task DeleteMany<T, T2>(T2 scope, IEnumerable<T> entity, string overrideCollectionName = "") where T : ScopedEntity<T2> where T2 : Entity, new()
        {
            foreach (var scopedEntity in entity)
            {
                scopedEntity.Scope = scope;
            }
            await DeleteMany<T>(entity); 
        }

        public async Task DeleteMany<T, T2>(T2 scope, List<string> IDs, string overrideCollectionName = "") where T : ScopedEntity<T2> where T2 : Entity, new()
        {
            if (IDs.Count == 0)
            {
                return;
            }

            await getCollection<T>(overrideCollectionName).DeleteManyAsync(f => f.Scope == scope && IDs.Contains(f.Id));
        }
        
        
        
        public async Task<T> ById<T, T2>(string scope, string id) where T : ScopedEntity<T2> where T2 : Entity, new()
        {
            var result = await getCollection<T>().FindOneAsync(e => e.Id == id && e.Scope == scope);
            return result;
        }

        public async Task<List<T>> ById<T, T2>(string scope, List<string> IDs) where T : ScopedEntity<T2> where T2 : Entity, new()
        {
            var result = await getCollection<T>().FindAsync(e => IDs.Contains(e.Id) && e.Scope == scope);
            return result.ToList();
        }

        public async Task<IQueryable<T>> All<T, T2>(string scope) where T : ScopedEntity<T2> where T2 : Entity, new()
        {
            var scopedEntities = getCollection<T>().AsQueryable().Where(f => f.Scope == scope);
            return await Task.Run(() => scopedEntities);
        }

        public async Task<T> One<T, T2>(string scope, Expression<Func<T, bool>> predicate) where T : ScopedEntity<T2> where T2 : Entity, new()
        {
            Expression<Func<T, bool>> firstPred = item => item.Scope == scope;
            var combinedPred = firstPred.And(predicate);
            var result = await getCollection<T>().FindOneAsync(combinedPred);

            return result;
        }

        public async Task<IQueryable<T>> Many<T, T2>(string scope, Expression<Func<T, bool>> predicate) where T : ScopedEntity<T2> where T2 : Entity, new()
        {
            var scopedEntities = getCollection<T>().AsQueryable().Where(f => f.Scope == scope).Where(predicate);

            return await Task.Run(() => scopedEntities);
        }

        public async Task<IQueryable<T>> Many<T, T2>(string scope, Expression<Func<T, bool>> predicate, int pageSize, int PageNumber) where T : ScopedEntity<T2> where T2 : Entity, new()
        {
            if (pageSize == 0 || PageNumber == 0)
            {
                return await Many<T,T2>(scope, predicate).ConfigureAwait(false);
            }

            var res = getCollection<T>().AsQueryable().Where(f => f.Scope == scope).Where(predicate).Skip((PageNumber - 1) * pageSize).Take(pageSize);
            return await Task.Run(() => res);
        }

        public async Task<long> CountMany<T, T2>(string scope, Expression<Func<T, bool>> predicate) where T : ScopedEntity<T2> where T2 : Entity, new()
        {
            Expression< Func<T, bool>>firstPred = item => item.Scope == scope;
            var combinedPred = firstPred.And(predicate);
            return await getCollection<T>().CountAsync(combinedPred);
        }

        public async Task Add<T, T2>(string scope, T entity, string overrideCollectionName = "") where T : ScopedEntity<T2> where T2 : Entity, new()
        {
            entity.Scope = scope;
            await Add(entity);
        }

        public async Task AddMany<T, T2>(string scope, IEnumerable<T> entities, string overrideCollectionName = "") where T : ScopedEntity<T2> where T2 : Entity, new()
        {
            foreach (var scopedEntity in entities)
            {
                scopedEntity.Scope = scope;
            }
            
            await AddMany(entities);
        }

        public async Task Update<T, T2>(string scope, T entity, string overrideCollectionName = "") where T : ScopedEntity<T2> where T2 : Entity, new()
        {
            entity.Scope = scope;
            await Update(entity);
        }

        public async Task UpdateMany<T, T2>(string scope, List<T> entity, string overrideCollectionName = "") where T : ScopedEntity<T2> where T2 : Entity, new()
        {
            entity.ForEach(f=>f.Scope = scope);
            await UpdateMany(entity);
        }

        public async Task Upsert<T, T2>(string scope, T entity, string overrideCollectionName = "") where T : ScopedEntity<T2> where T2 : Entity, new()
        {
            entity.Scope = scope;
            await Upsert(entity);
        }

        public async Task UpsertMany<T, T2>(string scope, List<T> entity, string overrideCollectionName = "") where T : ScopedEntity<T2> where T2 : Entity, new()
        {
            entity.ForEach(f=>f.Scope = scope);
            await UpsertMany(entity);
        }

        public async Task Delete<T, T2>(string scope, T entity, string overrideCollectionName = "") where T : ScopedEntity<T2> where T2 : Entity, new()
        {
            await Delete<T>(f => f.Scope == scope && f.Id == entity.Id);
        }

        public async Task Delete<T, T2>(string scope, string Id, string overrideCollectionName = "") where T : ScopedEntity<T2> where T2 : Entity, new()
        {
            await Delete<T>(f => f.Scope == scope && f.Id == Id);
        }

        public async Task DeleteMany<T, T2>(string scope, IEnumerable<T> entity, string overrideCollectionName = "") where T : ScopedEntity<T2> where T2 : Entity, new()
        {
            foreach (var scopedEntity in entity)
            {
                scopedEntity.Scope = scope;
            }

            await DeleteMany(entity);
        }

        public async Task DeleteMany<T, T2>(string scope, List<string> IDs, string overrideCollectionName = "") where T : ScopedEntity<T2> where T2 : Entity, new()
        {
            if (IDs.Count == 0)
            {
                return;
            }

            await getCollection<T>(overrideCollectionName).DeleteManyAsync(f => f.Scope == scope && IDs.Contains(f.Id));
        }
}