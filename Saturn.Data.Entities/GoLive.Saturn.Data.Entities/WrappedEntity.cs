namespace GoLive.Saturn.Data.Entities;

public class WrappedEntity<T> : Entity
{
    public static implicit operator T(WrappedEntity<T> someValue)
    {
        return someValue.Item;
    }

    public static implicit operator WrappedEntity<T>(T item)
    {
        return new WrappedEntity<T>() { Item = item };
    }
        
    public T Item { get; set; }
}