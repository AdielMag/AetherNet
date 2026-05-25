using System.Runtime.InteropServices;

namespace AetherNet.Collision;

[StructLayout(LayoutKind.Sequential)]
public struct CollisionFilter
{
    public ushort CategoryBits;
    public ushort MaskBits;
    public short  GroupIndex;

    public static CollisionFilter Default => new CollisionFilter { CategoryBits = 0x0001, MaskBits = 0xFFFF, GroupIndex = 0 };

    public static CollisionFilter FromLayer(int layer, int overrideMask = -1)
        => new CollisionFilter
        {
            CategoryBits = (ushort)(1 << layer),
            MaskBits     = (ushort)(overrideMask >= 0 ? overrideMask : PhysicsLayers.MaskFor(layer)),
            GroupIndex   = 0,
        };
}
