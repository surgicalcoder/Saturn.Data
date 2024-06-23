using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace GoLive.Saturn.Data.Entities;

public abstract class Entity : IEquatable<Entity>, INotifyPropertyChanged, IUniquelyIdentifiable
{
    public static bool TryParseId(string input, out string output)
    {
        if (string.IsNullOrWhiteSpace(input))
        {
            output = null;
            return true;
        }
            
        switch (input.Length)
        {
            case 16:
                output = BitConverter.ToString(Convert.FromBase64String(input)).Replace("-", "").ToLower();
                return true;
            case 24 when !IsHex(input):
                output = null;
                return false;
            case 24:
                output = input;
                return true;
            default:
                output = null;
                return false;
        }
    }

    public static string GetIdAsHex(string input)
    {
        Span<byte> resultantArray = stackalloc byte[12];
        ReadOnlySpan<char> inputSpan = input.AsSpan();
        for (int i = 0; i < 12; i++)
        {
            ReadOnlySpan<char> slice = inputSpan.Slice(i * 2, 2);
            if (byte.TryParse(slice, System.Globalization.NumberStyles.HexNumber, null, out byte value))
            {
                resultantArray[i] = value;
            }
            else
            {
                throw new FormatException($"Unable to parse '{slice.ToString()}' as a hexadecimal number.");
            }
        }
        return Convert.ToBase64String(resultantArray);
    }

    private static bool IsHex(ReadOnlySpan<char> chars)
    {
        for (int i = 0; i < chars.Length; i++)
        {
            char c = chars[i];
            bool isHex = c is >= '0' and <= '9' or >= 'a' and <= 'f' or >= 'A' and <= 'F';
            if (!isHex)
                return false;
        }
        return true;
    }

        
    private string id;
    private long? version;

    public virtual string Id
    {
        get => id;
        set
        {
            if (value != null)
            {
                var success = TryParseId(value, out string actualValue);

                if (success)
                {
                    value = actualValue;
                }
                else
                {
                    throw new InvalidCastException("Invalid ID");
                }
            }

            SetField(ref id, value);
        }
    }

    public virtual string _shortId => string.IsNullOrWhiteSpace(Id) ? null : GetIdAsHex(Id);


    public virtual long? Version
    {
        get => version;
        set => SetField(ref version, value);
    }

    public virtual Dictionary<string, object> Changes { get; set; } = new();

    public virtual bool EnableChangeTracking { get; set; }
        
    public virtual Dictionary<string, object> Properties { get; set; } = new();

    protected Entity()
    { }

    public virtual bool Equals(Entity other)
    {
        return other != null && Id == other.Id;
    }

    public virtual event PropertyChangedEventHandler PropertyChanged;

    protected virtual void OnPropertyChanged([CallerMemberName] string propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }

    protected virtual bool SetField<T>(ref T field, T newValue, [CallerMemberName] string propertyName = null)
    {
        if (EqualityComparer<T>.Default.Equals(field, newValue)) return false;

        field = newValue;
        OnPropertyChanged(propertyName);

        if (propertyName != null && Changes != null && EnableChangeTracking)
        {
            Changes[propertyName] = newValue;
        }

        return true;
    }
}