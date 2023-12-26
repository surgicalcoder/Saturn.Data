using System;
using System.Collections.Generic;

namespace GoLive.Saturn.Data.Entities;

public class WeakRef<T> : IComparable<WeakRef<T>> where T : Entity
{
    public T Item { get; set; }
    public string Id { get; set; }
    public WeakRef(string Id)
    {
        this.Id = Id;
    }

    public WeakRef(T reference)
    {
        Item = reference ?? throw new ArgumentNullException(nameof(reference));
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
        if (ReferenceEquals(this, other)) return 0;
        if (ReferenceEquals(null, other)) return 1;
        return string.Compare(Id, other.Id, StringComparison.InvariantCultureIgnoreCase);
    }

    protected bool Equals(WeakRef<T> other)
    {
        return string.Equals(Id, other.Id, StringComparison.InvariantCultureIgnoreCase);
    }

    public override bool Equals(object obj)
    {
        if (ReferenceEquals(null, obj)) return false;
        if (ReferenceEquals(this, obj)) return true;
        if (obj.GetType() != this.GetType()) return false;
        return Equals((WeakRef<T>)obj);
    }

    public override int GetHashCode()
    {
        return (Id != null ? StringComparer.InvariantCultureIgnoreCase.GetHashCode(Id) : 0);
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