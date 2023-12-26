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

    private static bool IsHex(IEnumerable<char> chars)
    {
        bool isHex; 
        foreach(var c in chars)
        {
            isHex = c is >= '0' and <= '9' or >= 'a' and <= 'f' or >= 'A' and <= 'F';

            if(!isHex)
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

    public virtual string _shortId
    {
        get
        {
            if (string.IsNullOrWhiteSpace(Id))
            {
                return null;
            }
            var resultantArray = new byte[12];
            resultantArray[0] = Convert.ToByte(Id[..2], 16);
            resultantArray[1] =  Convert.ToByte(Id[2..4], 16);
            resultantArray[2] =  Convert.ToByte(Id[4..6], 16);
            resultantArray[3] =  Convert.ToByte(Id[6..8], 16);
            resultantArray[4] =  Convert.ToByte(Id[8..10], 16);
            resultantArray[5] =  Convert.ToByte(Id[10..12], 16);
            resultantArray[6] =  Convert.ToByte(Id[12..14], 16);
            resultantArray[7] =  Convert.ToByte(Id[14..16], 16);
            resultantArray[8] =  Convert.ToByte(Id[16..18], 16);
            resultantArray[9] =  Convert.ToByte(Id[18..20], 16);
            resultantArray[10] = Convert.ToByte(Id[20..22], 16);
            resultantArray[11] = Convert.ToByte(Id[22..24], 16);

            return Convert.ToBase64String(resultantArray);
        }
    }
        

    public virtual long? Version
    {
        get => version;
        set => SetField(ref version, value);
    }

    public virtual Dictionary<string, object> Changes { get; set; } = new();

    public virtual bool EnableChangeTracking { get; set; }
        
    public virtual Dictionary<string, dynamic> Properties { get; set; } = new Dictionary<string, object>();

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

    protected virtual bool SetField<T>(ref T field, T value, [CallerMemberName] string propertyName = null)
    {
        if (EqualityComparer<T>.Default.Equals(field, value)) return false;

        field = value;
        OnPropertyChanged(propertyName);

        return true;
    }
}