namespace AetherNet.Queries;

public sealed class PhysicsQueryBuffer
{
    public readonly RaycastHit[]    RaycastResults;
    public readonly OverlapResult[] OverlapResults;
    public int RaycastCount  { get; internal set; }
    public int OverlapCount  { get; internal set; }

    public PhysicsQueryBuffer(int raycastCapacity = 32, int overlapCapacity = 64)
    {
        RaycastResults = new RaycastHit[raycastCapacity];
        OverlapResults = new OverlapResult[overlapCapacity];
    }

    internal void ClearRaycast() => RaycastCount = 0;
    internal void ClearOverlap() => OverlapCount  = 0;
}
