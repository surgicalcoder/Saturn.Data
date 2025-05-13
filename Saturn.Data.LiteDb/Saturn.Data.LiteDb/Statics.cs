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
            span = new byte[]
            {
                96,
                0
            };
        }
        else
        {
            span = new byte[]
            {
                0,
                96
            };
        }

        return MemoryMarshal.CreateReadOnlySpan(ref Unsafe.As<byte, char>(ref MemoryMarshal.GetReference(span)), 1);
    }
}