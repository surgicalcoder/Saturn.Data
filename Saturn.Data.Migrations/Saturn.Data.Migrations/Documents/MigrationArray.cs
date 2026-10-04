using System.Collections;
using System.Collections.Generic;

namespace GoLive.Saturn.Data.Migrations;

public sealed class MigrationArray : MigrationValue, IEnumerable<MigrationValue>
{
    private readonly List<MigrationValue> items = new();

    public MigrationArray() : base(MigrationValueKind.Array, null)
    {
    }

    public int Count => items.Count;

    public override MigrationArray AsArray() => this;

    public MigrationValue this[int index]
    {
        get => items[index];
        set => items[index] = value ?? Null;
    }

    public void Add(MigrationValue value) => items.Add(value ?? Null);

    public void RemoveAt(int index) => items.RemoveAt(index);

    public MigrationArray Clone()
    {
        var clone = new MigrationArray();

        foreach (var item in items)
        {
            clone.Add(item?.Clone() ?? Null);
        }

        return clone;
    }

    public IEnumerator<MigrationValue> GetEnumerator() => items.GetEnumerator();

    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();

    public bool Equals(MigrationArray other)
    {
        if (other is null || ReferenceEquals(this, other))
        {
            return ReferenceEquals(this, other);
        }

        if (Count != other.Count)
        {
            return false;
        }

        for (var i = 0; i < items.Count; i++)
        {
            if (!Equals(items[i], other.items[i]))
            {
                return false;
            }
        }

        return true;
    }

    public override bool Equals(object obj) => obj is MigrationArray other && Equals(other);

    public override int GetHashCode()
    {
        var hash = new HashCode();

        foreach (var item in items)
        {
            hash.Add(item?.GetHashCode() ?? 0);
        }

        return hash.ToHashCode();
    }
}
