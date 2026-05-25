using System.Runtime.InteropServices;
using SNV2 = System.Numerics.Vector2;

namespace AetherNet;

[StructLayout(LayoutKind.Sequential)]
public struct TransformState
{
    public SNV2  Position;
    public float Angle;
    public SNV2  LinearVelocity;
    public float AngularVelocity;
}
