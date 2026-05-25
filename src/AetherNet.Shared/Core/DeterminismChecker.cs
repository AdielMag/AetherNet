using System.Runtime.InteropServices;

namespace AetherNet;

public static class DeterminismChecker
{
    private const uint FnvOffset = 2166136261u;
    private const uint FnvPrime  = 16777619u;
    private static readonly int EntityStateSize = Marshal.SizeOf<EntityState>();

    public static unsafe uint ComputeHash(EntityState[] states, int count)
    {
        uint hash = FnvOffset;
        fixed (EntityState* ptr = states)
        {
            byte* bytes     = (byte*)ptr;
            int   byteCount = count * EntityStateSize;
            for (int i = 0; i < byteCount; i++)
                hash = (hash ^ bytes[i]) * FnvPrime;
        }
        return hash;
    }
}
