using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace Saturn.Data.MongoDb;

public class Statics
{
    public static ReadOnlySpan<char> Separator()
    {
        ReadOnlySpan<byte> span;
        span = BitConverter.IsLittleEndian ? "`\0"u8 : "\0`"u8;

        return MemoryMarshal.CreateReadOnlySpan(ref Unsafe.As<byte, char>(ref MemoryMarshal.GetReference(span)), 1);
    }
}