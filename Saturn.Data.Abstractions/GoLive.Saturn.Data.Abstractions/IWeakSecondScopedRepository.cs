using System;
using System.Collections.Generic;
using System.Linq.Expressions;
using System.Threading;
using System.Threading.Tasks;
using GoLive.Saturn.Data.Entities;

namespace GoLive.Saturn.Data.Abstractions;

public interface IWeakSecondScopedRepository : IWeakSecondScopedReadonlyRepository
{
    Task Delete<TItem>(string primaryScope, string secondScope, string id, IDatabaseTransaction transaction = null, CancellationToken cancellationToken = default)
        where TItem : Entity, ISecondScopedById, new();

    Task Delete<TItem>(string primaryScope, string secondScope, Expression<Func<TItem, bool>> filter, IDatabaseTransaction transaction = null, CancellationToken cancellationToken = default)
        where TItem : Entity, ISecondScopedById, new();

    Task Delete<TItem>(string primaryScope, string secondScope, IEnumerable<string> IDs, IDatabaseTransaction transaction = null, CancellationToken cancellationToken = default)
        where TItem : Entity, ISecondScopedById, new();

    Task Insert<TItem>(string primaryScope, string secondScope, TItem entity, IDatabaseTransaction transaction = null, CancellationToken cancellationToken = default)
        where TItem : Entity, ISecondScopedById, new();

    Task Insert<TItem>(string primaryScope, string secondScope, IEnumerable<TItem> entity, IDatabaseTransaction transaction = null, CancellationToken cancellationToken = default)
        where TItem : Entity, ISecondScopedById, new();

    Task JsonUpdate<TItem>(string primaryScope, string secondScope, string id, int version, string json, IDatabaseTransaction transaction = null,
        CancellationToken cancellationToken = default)
        where TItem : Entity, ISecondScopedById, new();

    Task Save<TItem>(string primaryScope, string secondScope, TItem entity, IDatabaseTransaction transaction = null, CancellationToken cancellationToken = default)
        where TItem : Entity, ISecondScopedById, new();

    Task Save<TItem>(string primaryScope, string secondScope, IEnumerable<TItem> entity, IDatabaseTransaction transaction = null, CancellationToken cancellationToken = default)
        where TItem : Entity, ISecondScopedById, new();

    Task Update<TItem>(string primaryScope, string secondScope, TItem entity, IDatabaseTransaction transaction = null, CancellationToken cancellationToken = default)
        where TItem : Entity, ISecondScopedById, new();

    Task Update<TItem>(string primaryScope, string secondScope, Expression<Func<TItem, bool>> conditionPredicate, TItem entity, IDatabaseTransaction transaction = null,
        CancellationToken cancellationToken = default)
        where TItem : Entity, ISecondScopedById, new();

    Task Update<TItem>(string primaryScope, string secondScope, IEnumerable<TItem> entity, IDatabaseTransaction transaction = null, CancellationToken cancellationToken = default)
        where TItem : Entity, ISecondScopedById, new();

    Task Upsert<TItem>(string primaryScope, string secondScope, TItem entity, IDatabaseTransaction transaction = null, CancellationToken cancellationToken = default)
        where TItem : Entity, ISecondScopedById, new();

    Task Upsert<TItem>(string primaryScope, string secondScope, IEnumerable<TItem> entity, IDatabaseTransaction transaction = null, CancellationToken cancellationToken = default)
        where TItem : Entity, ISecondScopedById, new();
}

