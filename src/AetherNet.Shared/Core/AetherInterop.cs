using System.Runtime.CompilerServices;
using AVec2 = nkast.Aether.Physics2D.Common.Vector2;
using SNV2  = System.Numerics.Vector2;

namespace AetherNet;

public static class AetherInterop
{
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static AVec2 ToAether(in SNV2 v) => new AVec2(v.X, v.Y);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static SNV2 FromAether(AVec2 v) => new SNV2(v.X, v.Y);
}
