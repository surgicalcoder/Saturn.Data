namespace GoLive.Saturn.Generator.Entities.Resources;

public interface ICreatableFrom<T>
{
    static abstract ICreatableFrom<T> Create(T input);
}