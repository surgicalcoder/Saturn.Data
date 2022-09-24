namespace GoLive.Saturn.Data.Entities
{
    public interface IUpdatableFrom<T>
    {
        void UpdateFrom(T input);
    }
}