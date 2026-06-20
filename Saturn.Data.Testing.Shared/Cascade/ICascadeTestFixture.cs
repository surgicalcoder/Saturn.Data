using GoLive.Saturn.Data.Abstractions;

namespace Saturn.Data.Testing.Shared.Cascade;

public interface ICascadeTestFixture<TRepository> : IRepositoryTestFixture<TRepository>
    where TRepository : IRepository
{
}
