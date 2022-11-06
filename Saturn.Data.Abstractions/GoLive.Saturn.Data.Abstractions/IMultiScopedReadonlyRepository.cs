using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Threading.Tasks;
using GoLive.Saturn.Data.Entities;

namespace GoLive.Saturn.Data.Abstractions
{
    public interface IMultiScopedReadonlyRepository : IScopedRepository
    {
        Task<T> ById<T, T2>(T2 PrimaryScope, IEnumerable<string> SecondaryScope, string id) where T : MultiscopedEntity<T2> where T2 : Entity, new();
        Task<T> ById<T, T2>(string PrimaryScope, IEnumerable<string> SecondaryScope, string id) where T : MultiscopedEntity<T2> where T2 : Entity, new();
        Task<IQueryable<T>> All<T, T2>(T2 PrimaryScope, IEnumerable<string> SecondaryScope) where T : MultiscopedEntity<T2> where T2 : Entity, new();
        Task<IQueryable<T>> All<T, T2>(string PrimaryScope, IEnumerable<string> SecondaryScope) where T : MultiscopedEntity<T2> where T2 : Entity, new();
        Task<T> One<T, T2>(T2 PrimaryScope, IEnumerable<string> SecondaryScope, Expression<Func<T, bool>> predicate) where T : MultiscopedEntity<T2> where T2 : Entity, new();
        Task<T> One<T, T2>(string PrimaryScope, IEnumerable<string> SecondaryScope, Expression<Func<T, bool>> predicate) where T : MultiscopedEntity<T2> where T2 : Entity, new();
        Task<IQueryable<T>> Many<T, T2>(T2 PrimaryScope, IEnumerable<string> SecondaryScope, Expression<Func<T, bool>> predicate, int pageSize=20, int PageNumber=1) where T : MultiscopedEntity<T2> where T2 : Entity, new();
        Task<IQueryable<T>> Many<T, T2>(string PrimaryScope, IEnumerable<string> SecondaryScope, Expression<Func<T, bool>> predicate, int pageSize=20, int PageNumber=1) where T : MultiscopedEntity<T2> where T2 : Entity, new();
        Task<long> CountMany<T, T2>(string PrimaryScope, IEnumerable<string> SecondaryScope, Expression<Func<T, bool>> predicate) where T : MultiscopedEntity<T2> where T2 : Entity, new();
    }
}