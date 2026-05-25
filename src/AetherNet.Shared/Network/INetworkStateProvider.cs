namespace AetherNet.Network;

public interface INetworkStateProvider
{
    void OnTickComplete(uint tick, EntityState[] states, int count);
    void ApplySnapshot(uint tick, EntityState[] states, int count);
}
