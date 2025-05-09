using System;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace GoLive.Saturn.Data;

public partial class Statics
{
    public static ReadOnlySpan<char> Separator()
    {
        ReadOnlySpan<byte> span;
        span = BitConverter.IsLittleEndian ? "`\0"u8 : "\0`"u8;

        return MemoryMarshal.CreateReadOnlySpan(ref Unsafe.As<byte, char>(ref MemoryMarshal.GetReference(span)), 1);
    }
}