using System.Runtime.InteropServices;
using SNV2 = System.Numerics.Vector2;

namespace AetherNet.Collision;

[StructLayout(LayoutKind.Sequential)]
public struct CollisionData
{
    public int   EntityIdA;
    public int   EntityIdB;
    public int   FixtureIndexA;
    public int   FixtureIndexB;
    public SNV2  ContactPoint;
    public SNV2  ContactNormal;
    public float Impulse;
    public uint  TickNumber;
}
