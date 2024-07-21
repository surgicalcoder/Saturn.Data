using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Threading.Tasks;
using GoLive.Saturn.Data.Entities;

namespace GoLive.Saturn.Data.Abstractions;

public interface IReadonlyRepository : IDisposable
{
    Task<T> ById<T>(string id) where T : Entity;
    Task<List<T>> ById<T>(List<string> IDs) where T : Entity;

    Task<List<Ref<T>>> ByRef<T>(List<Ref<T>> item) where T : Entity, new();
    Task<T> ByRef<T>(Ref<T> item) where T : Entity, new();
    Task<Ref<T>> PopulateRef<T>(Ref<T> item) where T : Entity, new();
        
    IQueryable<T> All<T>() where T : Entity;
        
    Task<T> One<T>(Expression<Func<T, bool>> predicate, IEnumerable<SortOrder<T>> sortOrders = null) where T : Entity;
    Task<T> Random<T>() where T : Entity;
    Task<List<T>> Random<T>(int count) where T : Entity;
        
    Task<IQueryable<T>> Many<T>(Expression<Func<T, bool>> predicate, IEnumerable<SortOrder<T>> sortOrders = null) where T : Entity;
    Task<List<T>> Many<T>(Dictionary<string, object> whereClause, IEnumerable<SortOrder<T>> sortOrders = null) where T : Entity;
    Task<IQueryable<T>> Many<T>(Expression<Func<T, bool>> predicate,int pageSize, int pageNumber, IEnumerable<SortOrder<T>> sortOrders = null) where T : Entity;
    Task<List<T>> Many<T>(Dictionary<string, object> whereClause, int pageSize, int pageNumber, IEnumerable<SortOrder<T>> sortOrders = null ) where T : Entity;
        
    Task<long> CountMany<T>(Expression<Func<T, bool>> predicate) where T : Entity;

    Task Watch<T>(Expression<Func<ChangedEntity<T>, bool>> predicate, ChangeOperation operationFilter, Action<T, string, ChangeOperation> callback) where T : Entity;
}