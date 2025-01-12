using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Threading.Tasks;
using GoLive.Saturn.Data.Abstractions;
using GoLive.Saturn.Data.Entities;
using MongoDB.Driver;
using SortDirection = GoLive.Saturn.Data.Abstractions.SortDirection;

namespace GoLive.Saturn.Data;

public partial class MongoDBRepository : ISecondScopedRepository
{
    public async Task<TItem> ById<TItem, TSecondScope, TPrimaryScope>(Ref<TPrimaryScope> primaryScope, Ref<TSecondScope> secondScope, string id) 
    where TItem : SecondScopedEntity<TSecondScope, TPrimaryScope>, new() 
    where TSecondScope : Entity, new() 
    where TPrimaryScope : Entity, new()
  {
    var result = await (await GetCollection<TItem>().FindAsync(e => e.Id == id && e.Scope == primaryScope && e.SecondScope == secondScope, new FindOptions<TItem> { Limit = 1 })).FirstOrDefaultAsync();

    return result;
  }

  public Task<IAsyncEnumerable<TItem>> All<TItem, TSecondScope, TPrimaryScope>(Ref<TPrimaryScope> primaryScope, Ref<TSecondScope> secondScope) 
    where TItem : SecondScopedEntity<TSecondScope, TPrimaryScope>, new() 
    where TSecondScope : Entity, new() 
    where TPrimaryScope : Entity, new()
  {
    var scopedEntities = GetCollection<TItem>().AsQueryable().Where(f => f.Scope == primaryScope && f.SecondScope == secondScope);

    return Task.FromResult(scopedEntities.ToAsyncEnumerable());
  }
  
  public IQueryable<TItem> IQueryable<TItem, TSecondScope, TPrimaryScope>(Ref<TPrimaryScope> primaryScope, Ref<TSecondScope> secondScope) where TItem : SecondScopedEntity<TSecondScope, TPrimaryScope>, new() where TSecondScope : Entity, new() where TPrimaryScope : Entity, new()
  {
    var scopedEntities = GetCollection<TItem>().AsQueryable().Where(f => f.Scope == primaryScope && f.SecondScope == secondScope);

    return scopedEntities;
  }

  public async Task<TItem> One<TItem, TSecondScope, TPrimaryScope>(Ref<TPrimaryScope> primaryScope, Ref<TSecondScope> secondScope, Expression<Func<TItem, bool>> predicate, IEnumerable<SortOrder<TItem>> sortOrders = null) 
    where TItem : SecondScopedEntity<TSecondScope, TPrimaryScope>, new() 
    where TSecondScope : Entity, new() 
    where TPrimaryScope : Entity, new()
  {
    Expression<Func<TItem, bool>> firstPred = item => item.Scope == primaryScope && item.SecondScope == secondScope;
    var combinedPred = firstPred.And(predicate);
        
    var findOptions = new FindOptions<TItem> { Limit = 1 };
    if (sortOrders != null && sortOrders.Any())
    {
      SortDefinition<TItem> sortDefinition = null;
      sortDefinition = getSortDefinition(sortOrders, sortDefinition);
      findOptions.Sort = sortDefinition;
    }
        
    var result = await GetCollection<TItem>().FindAsync(combinedPred, findOptions);

    return await result.FirstOrDefaultAsync();
  }
  
  public async Task<IQueryable<TItem>> Many<TItem, TSecondScope, TPrimaryScope>(Ref<TPrimaryScope> primaryScope, Ref<TSecondScope> secondScope, Expression<Func<TItem, bool>> predicate, int pageSize, int PageNumber, IEnumerable<SortOrder<TItem>> sortOrders = null) 
    where TItem : SecondScopedEntity<TSecondScope, TPrimaryScope>, new() 
    where TSecondScope : Entity, new() 
    where TPrimaryScope : Entity, new()
  {
    var res = GetCollection<TItem>().AsQueryable().Where(f => f.Scope == primaryScope && f.SecondScope == secondScope).Where(predicate);

    if (sortOrders != null)
    {
      res = sortOrders.Aggregate(res, (current, sortOrder) => sortOrder.Direction == SortDirection.Ascending ? current.OrderBy(sortOrder.Field) : current.OrderByDescending(sortOrder.Field));
    }

    if (pageSize != 0 && PageNumber != 0)
    {
      res = res.Skip((PageNumber - 1) * pageSize).Take(pageSize);
    }

    return await Task.Run(() => res);
  }

  public async Task<long> CountMany<TItem, TSecondScope, TPrimaryScope>(Ref<TPrimaryScope> primaryScope, Ref<TSecondScope> secondScope, Expression<Func<TItem, bool>> predicate) 
    where TItem : SecondScopedEntity<TSecondScope, TPrimaryScope>, new() 
    where TSecondScope : Entity, new() 
    where TPrimaryScope : Entity, new()
  {
    Expression<Func<TItem, bool>> firstPred = item => item.Scope == primaryScope && item.SecondScope == secondScope;
    var combinedPred = firstPred.And(predicate);

    return await GetCollection<TItem>().CountDocumentsAsync(combinedPred);
  }

  public async Task Insert<TItem, TSecondScope, TPrimaryScope>(Ref<TPrimaryScope> primaryScope, Ref<TSecondScope> secondScope, TItem entity) where TItem : SecondScopedEntity<TSecondScope, TPrimaryScope>, new() where TSecondScope : Entity, new() where TPrimaryScope : Entity, new()
  {
    entity.Scope = primaryScope;
    entity.SecondScope = secondScope;
    await Insert(entity);
  }

  public async Task Update<TItem, TSecondScope, TPrimaryScope>(Ref<TPrimaryScope> primaryScope, Ref<TSecondScope> secondScope, TItem entity) where TItem : SecondScopedEntity<TSecondScope, TPrimaryScope>, new() where TSecondScope : Entity, new() where TPrimaryScope : Entity, new()
  {
    entity.Scope = primaryScope;
    entity.SecondScope = secondScope;
    await Update(entity);
  }

  public async Task Upsert<TItem, TSecondScope, TPrimaryScope>(Ref<TPrimaryScope> primaryScope, Ref<TSecondScope> secondScope, TItem entity) where TItem : SecondScopedEntity<TSecondScope, TPrimaryScope>, new() where TSecondScope : Entity, new() where TPrimaryScope : Entity, new()
  {
    entity.Scope = primaryScope;
    entity.SecondScope = secondScope;
    await Upsert(entity);
  }

  public async Task Delete<TItem, TSecondScope, TPrimaryScope>(Ref<TPrimaryScope> primaryScope, Ref<TSecondScope> secondScope, string Id) where TItem : SecondScopedEntity<TSecondScope, TPrimaryScope>, new() where TSecondScope : Entity, new() where TPrimaryScope : Entity, new()
  {
    await Delete<TItem>(e => e.Scope == primaryScope && e.SecondScope == secondScope && e.Id == Id);
  }
}