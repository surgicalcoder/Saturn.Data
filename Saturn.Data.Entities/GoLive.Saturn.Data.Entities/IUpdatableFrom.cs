namespace GoLive.Saturn.Data.Entities;

public interface IUpdatableFrom<in T>
{
    void UpdateFrom(T input);
}