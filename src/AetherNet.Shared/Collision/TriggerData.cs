using System.Runtime.InteropServices;

namespace AetherNet.Collision;

[StructLayout(LayoutKind.Sequential)]
public struct TriggerData
{
    public int  TriggerEntityId;
    public int  OtherEntityId;
    public uint TickNumber;
}
