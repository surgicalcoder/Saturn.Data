using System;
using System.Collections.Generic;
using System.Linq.Expressions;
using System.Threading;
using System.Threading.Tasks;
using GoLive.Saturn.Data.Entities;

namespace GoLive.Saturn.Data.Abstractions;

public interface IRepository : IReadonlyRepository
{
    Task Insert<TItem>(TItem entity, IDatabaseTransaction transaction = null, CancellationToken cancellationToken = default) 
        where TItem : Entity;
    
    Task InsertMany<TItem>(IEnumerable<TItem> entities, IDatabaseTransaction transaction = null, CancellationToken cancellationToken = default) 
        where TItem : Entity;
        
    Task Save<TItem>(TItem entity, IDatabaseTransaction transaction = null, CancellationToken cancellationToken = default) 
        where TItem : Entity;
    
    Task SaveMany<TItem>(List<TItem> entities, IDatabaseTransaction transaction = null, CancellationToken cancellationToken = default) 
        where TItem : Entity;
        
    Task Update<TItem>(TItem entity, IDatabaseTransaction transaction = null, CancellationToken cancellationToken = default) 
        where TItem : Entity;
    
    Task Update<TItem>(Expression<Func<TItem, bool>> conditionPredicate, TItem entity, IDatabaseTransaction transaction = null, CancellationToken cancellationToken = default) 
        where TItem : Entity;
    
    Task UpdateMany<TItem>(List<TItem> entities, IDatabaseTransaction transaction = null, CancellationToken cancellationToken = default) 
        where TItem : Entity;

    Task Upsert<TItem>(TItem entity, IDatabaseTransaction transaction = null, CancellationToken cancellationToken = default) 
        where TItem : Entity;
    
    Task UpsertMany<TItem>(List<TItem> entity, IDatabaseTransaction transaction = null, CancellationToken cancellationToken = default) 
        where TItem : Entity;
        
    Task Delete<TItem>(TItem entity, IDatabaseTransaction transaction = null, CancellationToken cancellationToken = default) 
        where TItem : Entity;
    
    Task Delete<TItem>(Expression<Func<TItem, bool>> filter, IDatabaseTransaction transaction = null, CancellationToken cancellationToken = default) 
        where TItem : Entity;
    
    Task Delete<TItem>(string id, IDatabaseTransaction transaction = null, CancellationToken cancellationToken = default) 
        where TItem : Entity;
        
    Task DeleteMany<TItem>(IEnumerable<TItem> entities, IDatabaseTransaction transaction = null, CancellationToken cancellationToken = default) 
        where TItem : Entity;
    
    Task DeleteMany<TItem>(List<string> IDs, IDatabaseTransaction transaction = null, CancellationToken cancellationToken = default) 
        where TItem : Entity;
        
    Task JsonUpdate<TItem>(string id, int version, string json, IDatabaseTransaction transaction = null, CancellationToken cancellationToken = default) 
        where TItem : Entity;
    
    Task<IDatabaseTransaction> CreateTransaction();
}