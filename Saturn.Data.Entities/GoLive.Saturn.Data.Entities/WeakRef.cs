using System;
using System.Collections.Generic;

namespace GoLive.Saturn.Data.Entities;

public class WeakRef : IComparable<WeakRef>
{
    private string _refId;
    private Entity item;

    public virtual Entity Item
    {
        get => item;
        set => item = value;
    }

    public virtual string Id
    {
        get
        {
            if (Item != null && !string.IsNullOrWhiteSpace(Item.Id))
            {
                _refId = Item.Id;
                return Item.Id;
            }

            return _refId;
        }
        set => _refId = value;
    }

    public WeakRef(string Id)
    {
        _refId = Id;
    }

    public WeakRef(Entity reference)
    {
        item = reference ?? throw new ArgumentNullException(nameof(reference));

        if (!string.IsNullOrWhiteSpace(reference.Id))
        {
            _refId = reference.Id;
        }
    }

    public static implicit operator WeakRef(Entity item)
    {
        return new WeakRef(item);
    }

    public static implicit operator WeakRef(string item)
    {
        return new WeakRef(item);
    }

    public int CompareTo(WeakRef other)
    {
        if (ReferenceEquals(this, other)) return 0;
        if (ReferenceEquals(null, other)) return 1;
        return string.Compare(Id, other.Id, StringComparison.InvariantCultureIgnoreCase);
    }

    protected bool Equals(WeakRef other)
    {
        return string.Equals(Id, other.Id, StringComparison.InvariantCultureIgnoreCase);
    }

    public override bool Equals(object obj)
    {
        if (ReferenceEquals(null, obj)) return false;
        if (ReferenceEquals(this, obj)) return true;
        return obj is WeakRef other && Equals(other);
    }

    public override int GetHashCode()
    {
        return (Id != null ? StringComparer.InvariantCultureIgnoreCase.GetHashCode(Id) : 0);
    }

    public static bool operator ==(WeakRef left, WeakRef right)
    {
        return Equals(left, right);
    }

    public static bool operator ==(WeakRef left, string right)
    {
        if (left is null || String.IsNullOrWhiteSpace(right) || string.IsNullOrWhiteSpace(left.Id))
        {
            return false;
        }
        return left.Id.Equals(right, StringComparison.InvariantCultureIgnoreCase);
    }

    public static bool operator !=(WeakRef left, string right)
    {
        return !(left == right);
    }

    public static bool operator !=(WeakRef left, WeakRef right)
    {
        return !Equals(left, right);
    }

    public static bool operator <(WeakRef left, WeakRef right)
    {
        return Comparer<WeakRef>.Default.Compare(left, right) < 0;
    }

    public static bool operator >(WeakRef left, WeakRef right)
    {
        return Comparer<WeakRef>.Default.Compare(left, right) > 0;
    }

    public static bool operator <=(WeakRef left, WeakRef right)
    {
        return Comparer<WeakRef>.Default.Compare(left, right) <= 0;
    }

    public static bool operator >=(WeakRef left, WeakRef right)
    {
        return Comparer<WeakRef>.Default.Compare(left, right) >= 0;
    }
}

public class WeakRef<T> : WeakRef, IComparable<WeakRef<T>> where T : Entity
{
    public new T Item
    {
        get => (T)base.Item;
        set => base.Item = value;
    }

    public WeakRef(string Id) : base(Id)
    {
    }

    public WeakRef(T reference) : base(reference)
    {
    }

    public static implicit operator WeakRef<T>(T item)
    {
        return new WeakRef<T>(item);
    }

    public static implicit operator WeakRef<T>(string item)
    {
        return new WeakRef<T>(item);
    }

    public int CompareTo(WeakRef<T> other)
    {
        return base.CompareTo(other);
    }

    protected bool Equals(WeakRef<T> other)
    {
        return other is not null && string.Equals(Id, other.Id, StringComparison.InvariantCultureIgnoreCase);
    }

    public override bool Equals(object obj)
    {
        if (ReferenceEquals(null, obj)) return false;
        if (ReferenceEquals(this, obj)) return true;
        return obj is WeakRef other && string.Equals(Id, other.Id, StringComparison.InvariantCultureIgnoreCase);
    }

    public override int GetHashCode()
    {
        return base.GetHashCode();
    }

    public static bool operator ==(WeakRef<T> left, WeakRef<T> right)
    {
        return Equals(left, right);
    }

    public static bool operator ==(WeakRef<T> left, string right)
    {
        if (left is null || String.IsNullOrWhiteSpace(right) || string.IsNullOrWhiteSpace(left.Id))
        {
            return false;
        }
        return left.Id.Equals(right, StringComparison.InvariantCultureIgnoreCase);
    }

    public static bool operator !=(WeakRef<T> left, string right)
    {
        return !(left == right);
    }

    public static bool operator !=(WeakRef<T> left, WeakRef<T> right)
    {
        return !Equals(left, right);
    }

    public static bool operator <(WeakRef<T> left, WeakRef<T> right)
    {
        return Comparer<WeakRef<T>>.Default.Compare(left, right) < 0;
    }

    public static bool operator >(WeakRef<T> left, WeakRef<T> right)
    {
        return Comparer<WeakRef<T>>.Default.Compare(left, right) > 0;
    }

    public static bool operator <=(WeakRef<T> left, WeakRef<T> right)
    {
        return Comparer<WeakRef<T>>.Default.Compare(left, right) <= 0;
    }

    public static bool operator >=(WeakRef<T> left, WeakRef<T> right)
    {
        return Comparer<WeakRef<T>>.Default.Compare(left, right) >= 0;
    }
}