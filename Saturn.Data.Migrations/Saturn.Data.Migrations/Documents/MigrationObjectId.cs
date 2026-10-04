using System;
using GoLive.Saturn.Data.Entities;

namespace GoLive.Saturn.Data.Migrations;

public readonly struct MigrationObjectId : IEquatable<MigrationObjectId>
{
    private readonly byte[] bytes;

    public MigrationObjectId(byte[] value)
    {
        if (value == null || value.Length != 12)
        {
            throw new ArgumentException("An object id must contain exactly 12 bytes.", nameof(value));
        }

        bytes = (byte[])value.Clone();
    }

    public static MigrationObjectId Empty => default;

    public MigrationObjectId(string value)
    {
        if (!TryParse(value, out var parsed))
        {
            throw new FormatException($"'{value}' is not a valid object id.");
        }

        bytes = parsed.bytes;
    }

    public static MigrationObjectId NewObjectId()
    {
        return new MigrationObjectId(EntityIdGenerator.GenerateNewId());
    }

    public static bool TryParse(string input, out MigrationObjectId id)
    {
        id = default;

        if (!Entity.TryParseId(input, out var hex) || hex == null || hex.Length != 24)
        {
            return false;
        }

        var buffer = new byte[12];

        for (var i = 0; i < 12; i++)
        {
            var high = FromHex(hex[i * 2]);
            var low = FromHex(hex[i * 2 + 1]);

            if (high < 0 || low < 0)
            {
                return false;
            }

            buffer[i] = (byte)((high << 4) | low);
        }

        id = new MigrationObjectId(buffer);
        return true;
    }

    public byte[] ToByteArray()
    {
        return bytes == null ? new byte[12] : (byte[])bytes.Clone();
    }

    public bool Equals(MigrationObjectId other)
    {
        if (bytes == null || other.bytes == null)
        {
            return bytes == null && other.bytes == null;
        }

        return bytes.AsSpan().SequenceEqual(other.bytes);
    }

    public override bool Equals(object obj)
    {
        return obj is MigrationObjectId other && Equals(other);
    }

    public override int GetHashCode()
    {
        if (bytes == null)
        {
            return 0;
        }

        var hash = new HashCode();
        hash.AddBytes(bytes);
        return hash.ToHashCode();
    }

    public override string ToString()
    {
        if (bytes == null)
        {
            return null;
        }

        var chars = new char[24];

        for (var i = 0; i < 12; i++)
        {
            chars[i * 2] = ToHex((byte)(bytes[i] >> 4));
            chars[i * 2 + 1] = ToHex((byte)(bytes[i] & 0x0F));
        }

        return new string(chars);
    }

    public static bool operator ==(MigrationObjectId left, MigrationObjectId right)
    {
        return left.Equals(right);
    }

    public static bool operator !=(MigrationObjectId left, MigrationObjectId right)
    {
        return !left.Equals(right);
    }

    private static int FromHex(char value)
    {
        return value switch
        {
            >= '0' and <= '9' => value - '0',
            >= 'a' and <= 'f' => value - 'a' + 10,
            >= 'A' and <= 'F' => value - 'A' + 10,
            _ => -1
        };
    }

    private static char ToHex(byte value)
    {
        return (char)(value < 10 ? value + '0' : value + 'a' - 10);
    }
}
