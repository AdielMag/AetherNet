namespace AetherNet.Network;

public struct StateSnapshot
{
    public uint  TickNumber;
    public float SimulationTime;
    public int   EntityCount;
    public uint  DeterminismHash;
}
