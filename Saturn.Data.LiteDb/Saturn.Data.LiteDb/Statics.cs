using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace Saturn.Data.LiteDb;

public static class Statics
{
    public static ReadOnlySpan<char> Separator()
    {
        ReadOnlySpan<byte> span;

        if (BitConverter.IsLittleEndian)
        {
            span = "`\0"u8;
        }
        else
        {
            span = "\0`"u8;
        }

        return MemoryMarshal.CreateReadOnlySpan(ref Unsafe.As<byte, char>(ref MemoryMarshal.GetReference(span)), 1);
    }
}