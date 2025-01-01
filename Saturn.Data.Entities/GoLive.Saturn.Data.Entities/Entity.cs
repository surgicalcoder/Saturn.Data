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
                output = GetHexIdFromBase64(input);
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



    public static string GetHexIdFromBase64(string input)
    {
        // Typically for 12 bytes, base64 (standard) has length 16
        // If you support optional '=' padding or variable lengths,
        // adjust or remove this length check.
        if (input is null || input.Length != 16)
        {
            throw new ArgumentException("Input must be 16 characters long.", nameof(input));
        }

        // Convert the base64url string to standard Base64 in-place
        // '-' => '+', '_' => '/'
        Span<char> base64 = stackalloc char[16];
        for (int i = 0; i < input.Length; i++)
        {
            char c = input[i];
            base64[i] = c switch
            {
                '-' => '+',
                '_' => '/',
                _ => c
            };
        }

        // Decode from Base64 to 12 bytes
        Span<byte> bytes = stackalloc byte[12];
        if (!Convert.TryFromBase64Chars(base64, bytes, out int bytesDecoded) || bytesDecoded != 12)
        {
            // The input was invalid or didn't decode to exactly 12 bytes
            throw new FormatException("Invalid base64 or mismatch in expected byte length.");
        }

        // Convert those 12 bytes into 24 hex characters
        Span<char> hex = stackalloc char[24];
        for (int i = 0; i < 12; i++)
        {
            byte b = bytes[i];
            hex[i * 2] = ToHex((byte)(b >> 4));     // high nibble
            hex[i * 2 + 1] = ToHex((byte)(b & 0x0F));   // low nibble
        }

        return new string(hex);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static char ToHex(byte value)
    {
        // 0–9 => '0'–'9', 10–15 => 'a'–'f'
        return (char)(value < 10 ? value + '0' : value + 'a' - 10);
    }



    public static string GetIdAsHex(string input)
    {
        if (input == null || input.Length != 24)
        {
            throw new ArgumentException("Input must be exactly 24 characters long.", nameof(input));
        }

        // We'll decode 24 hex chars => 12 bytes
        Span<byte> bytes = stackalloc byte[12];

        // Convert hex to bytes, 2 chars => 1 byte
        for (int i = 0; i < 12; i++)
        {
            char c1 = input[i * 2];
            char c2 = input[i * 2 + 1];

            int hi = FromHex(c1);
            int lo = FromHex(c2);

            if (hi < 0 || lo < 0)
            {
                throw new FormatException($"Unable to parse '{c1}{c2}' as a hexadecimal number.");
            }

            bytes[i] = (byte)((hi << 4) | lo);
        }

        // Encode the 12 bytes as standard Base64
        // which will always produce 16 characters
        Span<char> base64 = stackalloc char[16];
        Convert.TryToBase64Chars(bytes, base64, out int len);

        // Convert '+' -> '-', '/' -> '_'
        for (int i = 0; i < len; i++)
        {
            switch (base64[i])
            {
                case '+':
                    base64[i] = '-';
                    break;
                case '/':
                    base64[i] = '_';
                    break;
            }
        }

        // Build the final string from 16 chars (some may be '='),
        // but typically for 12 bytes there's no '=' padding in standard base64 (it’d only appear at multiples of 3).
        // Even if '=' is present, it's still valid to keep them, or strip them if you wish.
        return new string(base64[..len]);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static int FromHex(char c) => c switch
    {
        >= '0' and <= '9' => c - '0',
        >= 'a' and <= 'f' => c - 'a' + 10,
        >= 'A' and <= 'F' => c - 'A' + 10,
        _ => -1
    };



    private static bool IsHex(ReadOnlySpan<char> chars)
    {
        for (int i = 0; i < chars.Length; i++)
        {
            char c = chars[i];
            // The three valid ranges are:
            //   '0'..'9'  => (c - '0') <= 9
            //   'A'..'F'  => (c - 'A') <= 5
            //   'a'..'f'  => (c - 'a') <= 5
            //
            // We cast to uint and compare to avoid negative checks.
            if (!((uint)(c - '0') <= 9
                  || (uint)(c - 'A') <= 5
                  || (uint)(c - 'a') <= 5))
            {
                return false;
            }
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