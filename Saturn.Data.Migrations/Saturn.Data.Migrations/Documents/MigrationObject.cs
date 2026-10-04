using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;

namespace GoLive.Saturn.Data.Migrations;

public sealed class MigrationObject : MigrationValue, IEnumerable<KeyValuePair<string, MigrationValue>>
{
    private readonly List<KeyValuePair<string, MigrationValue>> elements = new();

    public MigrationObject() : base(MigrationValueKind.Object, null)
    {
    }

    public int Count => elements.Count;

    public IEnumerable<string> Keys => elements.Select(e => e.Key);

    public override MigrationObject AsObject() => this;

    public bool ContainsKey(string name) => IndexOf(name) >= 0;

    public bool TryGetValue(string name, out MigrationValue value)
    {
        var index = IndexOf(name);

        if (index < 0)
        {
            value = null;
            return false;
        }

        value = elements[index].Value;
        return true;
    }

    public MigrationValue this[string name]
    {
        get => TryGetValue(name, out var value) ? value : null;
        set => Set(name, value);
    }

    public void Set(string name, MigrationValue value)
    {
        var index = IndexOf(name);

        if (index < 0)
        {
            elements.Add(new KeyValuePair<string, MigrationValue>(name, value ?? Null));
            return;
        }

        elements[index] = new KeyValuePair<string, MigrationValue>(name, value ?? Null);
    }

    public void Add(string name, MigrationValue value) => Set(name, value);

    public bool Remove(string name)
    {
        var index = IndexOf(name);

        if (index < 0)
        {
            return false;
        }

        elements.RemoveAt(index);
        return true;
    }

    public MigrationObject Clone()
    {
        var clone = new MigrationObject();

        foreach (var element in elements)
        {
            clone.Set(element.Key, element.Value?.Clone() ?? Null);
        }

        return clone;
    }

    public IEnumerator<KeyValuePair<string, MigrationValue>> GetEnumerator() => elements.GetEnumerator();

    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();

    public bool Equals(MigrationObject other)
    {
        if (other is null || ReferenceEquals(this, other))
        {
            return ReferenceEquals(this, other);
        }

        if (Count != other.Count)
        {
            return false;
        }

        for (var i = 0; i < elements.Count; i++)
        {
            if (!string.Equals(elements[i].Key, other.elements[i].Key, StringComparison.Ordinal))
            {
                return false;
            }

            if (!Equals(elements[i].Value, other.elements[i].Value))
            {
                return false;
            }
        }

        return true;
    }

    public override bool Equals(object obj) => obj is MigrationObject other && Equals(other);

    public override int GetHashCode()
    {
        var hash = new HashCode();

        foreach (var element in elements)
        {
            hash.Add(element.Key, StringComparer.Ordinal);
            hash.Add(element.Value?.GetHashCode() ?? 0);
        }

        return hash.ToHashCode();
    }

    private int IndexOf(string name)
    {
        for (var i = 0; i < elements.Count; i++)
        {
            if (string.Equals(elements[i].Key, name, StringComparison.Ordinal))
            {
                return i;
            }
        }

        return -1;
    }
}
