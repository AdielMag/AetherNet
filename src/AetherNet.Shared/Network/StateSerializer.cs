using System;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace AetherNet.Network;

public static class StateSerializer
{
    private static readonly int EntityStateSize = Marshal.SizeOf<EntityState>();

    public static int Serialize(EntityState[] states, int count, byte[] dst, int offset)
    {
        int bytesNeeded = count * EntityStateSize;
        if (dst.Length - offset < bytesNeeded) throw new ArgumentException("Destination buffer too small.");
        unsafe
        {
            fixed (EntityState* src = states)
            fixed (byte* dstPtr = dst)
                Buffer.MemoryCopy(src, dstPtr + offset, bytesNeeded, bytesNeeded);
        }
        return bytesNeeded;
    }

    public static int Deserialize(byte[] src, int offset, int byteCount, EntityState[] dst)
    {
        int count = byteCount / EntityStateSize;
        if (count > dst.Length) throw new ArgumentException("Destination array too small.");
        unsafe
        {
            fixed (byte* srcPtr = src)
            fixed (EntityState* dstPtr = dst)
                Buffer.MemoryCopy(srcPtr + offset, dstPtr, byteCount, byteCount);
        }
        return count;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static int PayloadSize(int entityCount)
        => sizeof(uint) + sizeof(float) + sizeof(int) + sizeof(uint) + entityCount * EntityStateSize;
}
