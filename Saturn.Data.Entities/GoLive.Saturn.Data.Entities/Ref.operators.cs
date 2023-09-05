namespace GoLive.Saturn.Data.Entities;

public partial class Ref<T>
{
    
    public static implicit operator string(Ref<T> item)
    {
        if (item == null)
        {
            return null;
        }

        if (item.Item != null && !string.IsNullOrWhiteSpace(item.Item.Id))
        {
            return item.Item.Id;
        }
            
        if (!string.IsNullOrWhiteSpace(item.Id))
        {
            return item.Id;
        }

        return null;
    }

    public static implicit operator T(Ref<T> item)
    {
        if (item == null)
        {
            return null;
        }

        if (item.Item != null)
        {
            return item.Item;
        }

        return item.Id != null ? new T(){Id = item.Id} : null;
    }

    public static implicit operator Ref<T>(T item)
    {
        return item == default ? default : new Ref<T>() { Item = item };
    }

    public static implicit operator Ref<T>(string item)
    {
        return string.IsNullOrWhiteSpace(item) ? default : new Ref<T>(item);
    }

    public static implicit operator Entity(Ref<T> item)
    {
        return item?.Item;
    }
    
    
    public bool Equals(Ref<T> other)
    {
        if (ReferenceEquals(null, other)) return false;
        if (ReferenceEquals(this, other)) return true;
        return string.Equals(_refId, other._refId);
    }

    public override bool Equals(object obj)
    {
        if (ReferenceEquals(null, obj)) return false;
        if (ReferenceEquals(this, obj)) return true;
        if (obj.GetType() != this.GetType()) return false;
        return Equals((Ref<T>)obj);
    }

    public override int GetHashCode()
    {
        return (_refId != null ? _refId.GetHashCode() : 0);
    }

    public static bool operator ==(Ref<T> left, Ref<T> right)
    {
        return Equals(left, right);
    }

    public static bool operator !=(Ref<T> left, Ref<T> right)
    {
        return !Equals(left, right);
    }

}