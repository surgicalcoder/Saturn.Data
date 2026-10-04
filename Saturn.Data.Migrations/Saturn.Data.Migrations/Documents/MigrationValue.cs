using System;

namespace GoLive.Saturn.Data.Migrations;

public class MigrationValue
{
    private readonly object payload;

    protected MigrationValue(MigrationValueKind kind, object payload)
    {
        Kind = kind;
        this.payload = payload;
    }

    public MigrationValueKind Kind { get; }

    public static readonly MigrationValue Null = new(MigrationValueKind.Null, null);
    public static readonly MigrationValue MinValue = new(MigrationValueKind.MinValue, null);
    public static readonly MigrationValue MaxValue = new(MigrationValueKind.MaxValue, null);
    public static readonly MigrationValue True = new(MigrationValueKind.Boolean, true);
    public static readonly MigrationValue False = new(MigrationValueKind.Boolean, false);

    public bool IsNull => Kind == MigrationValueKind.Null;
    public bool IsObject => Kind == MigrationValueKind.Object;
    public bool IsArray => Kind == MigrationValueKind.Array;
    public bool IsBinary => Kind == MigrationValueKind.Binary;
    public bool IsString => Kind == MigrationValueKind.String;
    public bool IsObjectId => Kind == MigrationValueKind.ObjectId;
    public bool IsGuid => Kind == MigrationValueKind.Guid;
    public bool IsBoolean => Kind == MigrationValueKind.Boolean;
    public bool IsNumber => Kind is MigrationValueKind.Int32 or MigrationValueKind.Int64 or MigrationValueKind.Double or MigrationValueKind.Decimal;
    public bool IsScalar => Kind is not (MigrationValueKind.Object or MigrationValueKind.Array);

    public static MigrationValue From(bool value) => value ? True : False;
    public static MigrationValue From(int value) => new(MigrationValueKind.Int32, value);
    public static MigrationValue From(long value) => new(MigrationValueKind.Int64, value);
    public static MigrationValue From(double value) => new(MigrationValueKind.Double, value);
    public static MigrationValue From(decimal value) => new(MigrationValueKind.Decimal, value);
    public static MigrationValue From(string value) => value == null ? Null : new(MigrationValueKind.String, value);
    public static MigrationValue From(DateTime value) => new(MigrationValueKind.DateTime, value);
    public static MigrationValue From(Guid value) => new(MigrationValueKind.Guid, value);
    public static MigrationValue From(MigrationObjectId value) => new(MigrationValueKind.ObjectId, value);
    public static MigrationValue From(byte[] value) => value == null ? Null : new(MigrationValueKind.Binary, value);

    public virtual MigrationObject AsObject() => throw new InvalidOperationException($"Value of kind '{Kind}' is not an object.");

    public virtual MigrationArray AsArray() => throw new InvalidOperationException($"Value of kind '{Kind}' is not an array.");

    public string AsString => Kind switch
    {
        MigrationValueKind.String => (string)payload,
        MigrationValueKind.ObjectId => ((MigrationObjectId)payload).ToString(),
        MigrationValueKind.Null => null,
        _ => payload?.ToString()
    };

    public int AsInt32 => Kind switch
    {
        MigrationValueKind.Int32 => (int)payload,
        MigrationValueKind.Int64 => (int)(long)payload,
        MigrationValueKind.Double => (int)(double)payload,
        MigrationValueKind.Decimal => (int)(decimal)payload,
        _ => throw new InvalidOperationException($"Value of kind '{Kind}' is not numeric.")
    };

    public long AsInt64 => Kind switch
    {
        MigrationValueKind.Int32 => (int)payload,
        MigrationValueKind.Int64 => (long)payload,
        MigrationValueKind.Double => (long)(double)payload,
        MigrationValueKind.Decimal => (long)(decimal)payload,
        _ => throw new InvalidOperationException($"Value of kind '{Kind}' is not numeric.")
    };

    public double AsDouble => Kind switch
    {
        MigrationValueKind.Int32 => (int)payload,
        MigrationValueKind.Int64 => (long)payload,
        MigrationValueKind.Double => (double)payload,
        MigrationValueKind.Decimal => (double)(decimal)payload,
        _ => throw new InvalidOperationException($"Value of kind '{Kind}' is not numeric.")
    };

    public decimal AsDecimal => Kind switch
    {
        MigrationValueKind.Int32 => (int)payload,
        MigrationValueKind.Int64 => (long)payload,
        MigrationValueKind.Double => (decimal)(double)payload,
        MigrationValueKind.Decimal => (decimal)payload,
        _ => throw new InvalidOperationException($"Value of kind '{Kind}' is not numeric.")
    };

    public bool AsBoolean => Kind switch
    {
        MigrationValueKind.Boolean => (bool)payload,
        _ => throw new InvalidOperationException($"Value of kind '{Kind}' is not boolean.")
    };

    public DateTime AsDateTime => Kind switch
    {
        MigrationValueKind.DateTime => (DateTime)payload,
        _ => throw new InvalidOperationException($"Value of kind '{Kind}' is not a date/time.")
    };

    public MigrationObjectId AsObjectId
    {
        get
        {
            if (Kind == MigrationValueKind.ObjectId)
            {
                return (MigrationObjectId)payload;
            }

            if (Kind == MigrationValueKind.String && MigrationObjectId.TryParse((string)payload, out var parsed))
            {
                return parsed;
            }

            throw new InvalidOperationException($"Value of kind '{Kind}' is not an object id.");
        }
    }

    public Guid AsGuid => Kind switch
    {
        MigrationValueKind.Guid => (Guid)payload,
        MigrationValueKind.String when Guid.TryParse((string)payload, out var parsed) => parsed,
        _ => throw new InvalidOperationException($"Value of kind '{Kind}' is not a guid.")
    };

    public byte[] AsBinary => Kind switch
    {
        MigrationValueKind.Binary => (byte[])payload,
        _ => throw new InvalidOperationException($"Value of kind '{Kind}' is not binary.")
    };

    public MigrationValue Clone() => Kind switch
    {
        MigrationValueKind.Object => AsObject().Clone(),
        MigrationValueKind.Array => AsArray().Clone(),
        MigrationValueKind.Binary => From((byte[])payload),
        _ => this
    };

    public bool Equals(MigrationValue other)
    {
        if (other is null)
        {
            return false;
        }

        if (ReferenceEquals(this, other))
        {
            return true;
        }

        if (Kind != other.Kind)
        {
            return false;
        }

        return Kind switch
        {
            MigrationValueKind.Null or MigrationValueKind.MinValue or MigrationValueKind.MaxValue => true,
            MigrationValueKind.Object => AsObject().Equals(other.AsObject()),
            MigrationValueKind.Array => AsArray().Equals(other.AsArray()),
            MigrationValueKind.Binary => ((byte[])payload).AsSpan().SequenceEqual((byte[])other.payload),
            _ => Equals(payload, other.payload)
        };
    }

    public override bool Equals(object obj) => obj is MigrationValue other && Equals(other);

    public override int GetHashCode()
    {
        return Kind switch
        {
            MigrationValueKind.Object => AsObject().GetHashCode(),
            MigrationValueKind.Array => AsArray().GetHashCode(),
            MigrationValueKind.Binary => HashCode.Combine(Kind, ((byte[])payload).Length),
            _ => HashCode.Combine(Kind, payload)
        };
    }

    public static bool operator ==(MigrationValue left, MigrationValue right) => left is null ? right is null : left.Equals(right);

    public static bool operator !=(MigrationValue left, MigrationValue right) => !(left == right);
}
