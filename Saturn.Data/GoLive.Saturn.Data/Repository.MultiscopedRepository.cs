using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Threading.Tasks;
using GoLive.Saturn.Data.Abstractions;
using GoLive.Saturn.Data.Entities;
using MongoDB.Driver;

namespace GoLive.Saturn.Data
{
    public partial class Repository : IMultiScopedReadonlyRepository
    {
        public async Task<T> ById<T, T2>(T2 PrimaryScope, IEnumerable<string> SecondaryScope, string id) where T : MultiscopedEntity<T2> where T2 : Entity, new()
        {
            var result = await (await GetCollection<T>().FindAsync(e => e.Id == id && e.Scope == PrimaryScope && SecondaryScope.All(f=>e.Scopes.Contains(f)), new FindOptions<T> { Limit = 1 })).FirstOrDefaultAsync();
            return result;
        }

        public async Task<T> ById<T, T2>(string PrimaryScope, IEnumerable<string> SecondaryScope, string id) where T : MultiscopedEntity<T2> where T2 : Entity, new()
        {
            var result = await (await GetCollection<T>().FindAsync(e => e.Id == id && e.Scope == PrimaryScope && SecondaryScope.All(f=>e.Scopes.Contains(f)), new FindOptions<T> { Limit = 1 })).FirstOrDefaultAsync();
            return result;
        }

        public async Task<IQueryable<T>> All<T, T2>(T2 PrimaryScope, IEnumerable<string> SecondaryScope) where T : MultiscopedEntity<T2> where T2 : Entity, new()
        {
            var scopedEntities = GetCollection<T>().AsQueryable().Where(e => e.Scope == PrimaryScope && SecondaryScope.All(f=>e.Scopes.Contains(f)));
            return await Task.Run(() => scopedEntities);
        }

        public async Task<IQueryable<T>> All<T, T2>(string PrimaryScope, IEnumerable<string> SecondaryScope) where T : MultiscopedEntity<T2> where T2 : Entity, new()
        {
            var scopedEntities = GetCollection<T>().AsQueryable().Where(e => e.Scope == PrimaryScope && SecondaryScope.All(f=>e.Scopes.Contains(f)));
            return await Task.Run(() => scopedEntities);
        }

        public async Task<T> One<T, T2>(T2 PrimaryScope, IEnumerable<string> SecondaryScope, Expression<Func<T, bool>> predicate) where T : MultiscopedEntity<T2> where T2 : Entity, new()
        {
            Expression<Func<T, bool>> firstPred = item => item.Scope == PrimaryScope && SecondaryScope.All(f=>item.Scopes.Contains(f));
            var combinedPred = firstPred.And(predicate);
            var result = await GetCollection<T>().FindAsync(combinedPred, new FindOptions<T> { Limit = 1 });
            return await result.FirstOrDefaultAsync();
        }

        public async Task<T> One<T, T2>(string PrimaryScope, IEnumerable<string> SecondaryScope, Expression<Func<T, bool>> predicate) where T : MultiscopedEntity<T2> where T2 : Entity, new()
        {
            Expression<Func<T, bool>> firstPred = item => item.Scope == PrimaryScope && SecondaryScope.All(f=>item.Scopes.Contains(f));
            var combinedPred = firstPred.And(predicate);
            var result = await GetCollection<T>().FindAsync(combinedPred, new FindOptions<T> { Limit = 1 });
            return await result.FirstOrDefaultAsync();
        }

        public async Task<IQueryable<T>> Many<T, T2>(T2 PrimaryScope, IEnumerable<string> SecondaryScope, Expression<Func<T, bool>> predicate, int pageSize = 20, int PageNumber = 1) where T : MultiscopedEntity<T2> where T2 : Entity, new()
        {
            var scopedEntities = GetCollection<T>().AsQueryable().Where(e => e.Scope == PrimaryScope && SecondaryScope.All(f=>e.Scopes.Contains(f))).Where(predicate);
            return await Task.Run(() => scopedEntities);
        }

        public async Task<IQueryable<T>> Many<T, T2>(string PrimaryScope, IEnumerable<string> SecondaryScope, Expression<Func<T, bool>> predicate, int pageSize = 20, int PageNumber = 1) where T : MultiscopedEntity<T2> where T2 : Entity, new()
        {
            var scopedEntities = GetCollection<T>().AsQueryable().Where(e => e.Scope == PrimaryScope && SecondaryScope.All(f=>e.Scopes.Contains(f))).Where(predicate);
            return await Task.Run(() => scopedEntities);
        }

        public async Task<long> CountMany<T, T2>(string PrimaryScope, IEnumerable<string> SecondaryScope, Expression<Func<T, bool>> predicate) where T : MultiscopedEntity<T2> where T2 : Entity, new()
        {
            Expression< Func<T, bool>>firstPred = item => item.Scope == PrimaryScope && SecondaryScope.All(f=>item.Scopes.Contains(f));
            var combinedPred = firstPred.And(predicate);
            return await GetCollection<T>().CountDocumentsAsync(combinedPred);
        }
    }
}