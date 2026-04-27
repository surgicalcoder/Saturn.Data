namespace Saturn.Data.Testing.Shared;

public interface IRepositoryTestFixture<out TRepository>
{
    TRepository Repository { get; }
}

