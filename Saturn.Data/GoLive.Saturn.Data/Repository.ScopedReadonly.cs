﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Threading.Tasks;
using GoLive.Saturn.Data.Abstractions;
using GoLive.Saturn.Data.Entities;
using MongoDB.Driver;

namespace GoLive.Saturn.Data;

public partial class Repository : IScopedReadonlyRepository
{
    public async Task<T> ById<T, T2>(T2 scope, string id) where T : ScopedEntity<T2> where T2 : Entity, new()
        {
            var result = await (await GetCollection<T>().FindAsync(e => e.Id == id && e.Scope == scope, new FindOptions<T> { Limit = 1 })).FirstOrDefaultAsync();

            return result;
        }

        public async Task<List<T>> ById<T, T2>(T2 scope, List<string> IDs) where T : ScopedEntity<T2> where T2 : Entity, new()
        {
            var result = await GetCollection<T>().FindAsync(e => IDs.Contains(e.Id) && e.Scope == scope);
            return await result.ToListAsync().ConfigureAwait(false);
        }

        public async Task<IQueryable<T>> All<T, T2>(T2 scope) where T : ScopedEntity<T2> where T2 : Entity, new()
        {
            var scopedEntities = GetCollection<T>().AsQueryable().Where(f => f.Scope == scope);
            return await Task.Run(() => scopedEntities);
        }

        public async Task<T> One<T, T2>(T2 scope, Expression<Func<T, bool>> predicate) where T : ScopedEntity<T2> where T2 : Entity, new()
        {
            Expression<Func<T, bool>> firstPred = item => item.Scope == scope;
            var combinedPred = firstPred.And(predicate);
            SortDefinitionBuilder<T> sdb = new SortDefinitionBuilder<T>();
            BLARG();
            var result = await GetCollection<T>().FindAsync(combinedPred, new FindOptions<T> { Limit = 1, Sort = sdb.Combine() });

            return await result.FirstOrDefaultAsync();
        }

        public async Task<IQueryable<T>> Many<T, T2>(T2 scope, Expression<Func<T, bool>> predicate) where T : ScopedEntity<T2> where T2 : Entity, new()
        {
            var scopedEntities = GetCollection<T>().AsQueryable().Where(f => f.Scope == scope).Where(predicate);

            return await Task.Run(() => scopedEntities);
        }

        public async Task<IQueryable<T>> Many<T, T2>(T2 scope, Expression<Func<T, bool>> predicate, int pageSize, int PageNumber) where T : ScopedEntity<T2> where T2 : Entity, new()
        {
            if (pageSize == 0 || PageNumber == 0)
            {
                return await Many(scope, predicate).ConfigureAwait(false);
            }

            var res = GetCollection<T>().AsQueryable().Where(f => f.Scope == scope).Where(predicate).Skip((PageNumber - 1) * pageSize).Take(pageSize);
            return await Task.Run(() => res);
        }

        public async Task<long> CountMany<T, T2>(T2 scope, Expression<Func<T, bool>> predicate) where T : ScopedEntity<T2> where T2 : Entity, new()
        {
           Expression< Func<T, bool>>firstPred = item => item.Scope == scope;
           var combinedPred = firstPred.And(predicate);
            
            return await GetCollection<T>().CountDocumentsAsync(combinedPred);
        }
        
        public async Task<T> ById<T, T2>(string scope, string id) where T : ScopedEntity<T2> where T2 : Entity, new()
        {
            var result = await (await GetCollection<T>().FindAsync(e => e.Id == id && e.Scope == scope, new FindOptions<T> { Limit = 1 })).FirstOrDefaultAsync();
            return result;
        }

        public async Task<List<T>> ById<T, T2>(string scope, List<string> IDs) where T : ScopedEntity<T2> where T2 : Entity, new()
        {
            var result = await GetCollection<T>().FindAsync(e => IDs.Contains(e.Id) && e.Scope == scope);
            return await result.ToListAsync().ConfigureAwait(false);
        }

        public async Task<IQueryable<T>> All<T, T2>(string scope) where T : ScopedEntity<T2> where T2 : Entity, new()
        {
            var scopedEntities = GetCollection<T>().AsQueryable().Where(f => f.Scope == scope);
            return await Task.Run(() => scopedEntities);
        }

        public async Task<T> One<T, T2>(string scope, Expression<Func<T, bool>> predicate) where T : ScopedEntity<T2> where T2 : Entity, new()
        {
            Expression<Func<T, bool>> firstPred = item => item.Scope == scope;
            var combinedPred = firstPred.And(predicate);
            var result = await GetCollection<T>().FindAsync(combinedPred, new FindOptions<T> { Limit = 1 });

            return await result.FirstOrDefaultAsync();
        }

        public async Task<IQueryable<T>> Many<T, T2>(string scope, Expression<Func<T, bool>> predicate) where T : ScopedEntity<T2> where T2 : Entity, new()
        {
            var scopedEntities = GetCollection<T>().AsQueryable().Where(f => f.Scope == scope).Where(predicate);

            return await Task.Run(() => scopedEntities);
        }

        public async Task<IQueryable<T>> Many<T, T2>(string scope, Expression<Func<T, bool>> predicate, int pageSize, int PageNumber) where T : ScopedEntity<T2> where T2 : Entity, new()
        {
            if (pageSize == 0 || PageNumber == 0)
            {
                return await Many<T,T2>(scope, predicate).ConfigureAwait(false);
            }

            var res = GetCollection<T>().AsQueryable().Where(f => f.Scope == scope).Where(predicate).Skip((PageNumber - 1) * pageSize).Take(pageSize);
            return await Task.Run(() => res);
        }

        public async Task<long> CountMany<T, T2>(string scope, Expression<Func<T, bool>> predicate) where T : ScopedEntity<T2> where T2 : Entity, new()
        {
            Expression< Func<T, bool>>firstPred = item => item.Scope == scope;
            var combinedPred = firstPred.And(predicate);
            return await GetCollection<T>().CountDocumentsAsync(combinedPred);
        }
}