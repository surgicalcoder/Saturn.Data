using System;
using System.Threading;

namespace GoLive.Saturn.Data.Entities;

// Based on MongoDB ObjectId generation strategy
public static class EntityIdGenerator
{
    private static readonly DateTime unixEpoch = new(1970, 1, 1, 0, 0, 0, DateTimeKind.Utc);
    private static int staticIncrement = (new Random()).Next();

    public static string GenerateNewId()
    {
        // Get timestamp
        var secondsSinceEpoch = (long)Math.Floor((DateTime.UtcNow - unixEpoch).TotalSeconds);
        if (secondsSinceEpoch is < uint.MinValue or > uint.MaxValue)
        {
            throw new ArgumentOutOfRangeException(nameof(DateTime.UtcNow));
        }
        var timestamp = (int)(uint)secondsSinceEpoch;

        // Calculate random value
        var machineName = Environment.MachineName;
        var machineHash = 0x00ffffff & machineName.GetHashCode();
        var seed = (int)DateTime.UtcNow.Ticks ^ machineHash ^ Environment.ProcessId;
        var random = new Random(seed);
        var high = random.Next();
        var low = random.Next();
        var combined = (long)((ulong)(uint)high << 32 | (ulong)(uint)low);
        var randomValue = combined & 0xffffffffff; // low order 5 bytes

        // Get increment
        int increment = Interlocked.Increment(ref staticIncrement) & 0x00ffffff;

        // Validate ranges
        if (randomValue is < 0 or > 0xffffffffff)
        {
            throw new ArgumentOutOfRangeException(nameof(randomValue), "The random value must be between 0 and 1099511627775 (it must fit in 5 bytes).");
        }
        if (increment is < 0 or > 0xffffff)
        {
            throw new ArgumentOutOfRangeException(nameof(increment), "The increment value must be between 0 and 16777215 (it must fit in 3 bytes).");
        }

        // Create object id components
        var a = timestamp;
        var b = (int)(randomValue >> 8); // first 4 bytes of random
        var c = (int)(randomValue << 24) | increment; // 5th byte of random and 3 byte increment

        // Convert to hex string
        var chars = new char[24];
        chars[0] = ToHexChar((a >> 28) & 0x0f);
        chars[1] = ToHexChar((a >> 24) & 0x0f);
        chars[2] = ToHexChar((a >> 20) & 0x0f);
        chars[3] = ToHexChar((a >> 16) & 0x0f);
        chars[4] = ToHexChar((a >> 12) & 0x0f);
        chars[5] = ToHexChar((a >> 8) & 0x0f);
        chars[6] = ToHexChar((a >> 4) & 0x0f);
        chars[7] = ToHexChar(a & 0x0f);
        chars[8] = ToHexChar((b >> 28) & 0x0f);
        chars[9] = ToHexChar((b >> 24) & 0x0f);
        chars[10] = ToHexChar((b >> 20) & 0x0f);
        chars[11] = ToHexChar((b >> 16) & 0x0f);
        chars[12] = ToHexChar((b >> 12) & 0x0f);
        chars[13] = ToHexChar((b >> 8) & 0x0f);
        chars[14] = ToHexChar((b >> 4) & 0x0f);
        chars[15] = ToHexChar(b & 0x0f);
        chars[16] = ToHexChar((c >> 28) & 0x0f);
        chars[17] = ToHexChar((c >> 24) & 0x0f);
        chars[18] = ToHexChar((c >> 20) & 0x0f);
        chars[19] = ToHexChar((c >> 16) & 0x0f);
        chars[20] = ToHexChar((c >> 12) & 0x0f);
        chars[21] = ToHexChar((c >> 8) & 0x0f);
        chars[22] = ToHexChar((c >> 4) & 0x0f);
        chars[23] = ToHexChar(c & 0x0f);

        return new string(chars);

        static char ToHexChar(int value)
        {
            return (char)(value + (value < 10 ? '0' : 'a' - 10));
        }
    }
}