using System;
using System.Diagnostics;
using System.Threading;
using AetherNet;
using AetherNet.Collision;

namespace AetherNet.Server;

public sealed class ServerTickLoop : IFullCollisionSink
{
    private readonly PhysicsWorldManager _world;
    private readonly EntityState[]       _snapshotBuffer;
    private Action<EntityState[], int, uint>? _onSnapshot;

    public ServerTickLoop(PhysicsWorldManager world)
    {
        _world          = world;
        _snapshotBuffer = new EntityState[SimulationConstants.MaxBodies];
    }

    public void SetSnapshotCallback(Action<EntityState[], int, uint> callback)
        => _onSnapshot = callback;

    public void Run(CancellationToken ct)
    {
        long targetTicks = (long)(SimulationConstants.FixedTimestep * Stopwatch.Frequency);
        var  sw          = Stopwatch.StartNew();
        long lastTicks   = sw.ElapsedTicks;

        while (!ct.IsCancellationRequested)
        {
            long  now = sw.ElapsedTicks;
            float dt  = (float)((now - lastTicks) / (double)Stopwatch.Frequency);
            lastTicks = now;

            _world.Advance(dt);
            _world.Events.DrainAll(this);

            _world.CopyStateTo(_snapshotBuffer, out int count);
            _onSnapshot?.Invoke(_snapshotBuffer, count, _world.TickNumber);

            long elapsed   = sw.ElapsedTicks - now;
            long remaining = targetTicks - elapsed;
            if (remaining > 0)
            {
                int sleepMs = (int)(remaining * 1000L / Stopwatch.Frequency);
                if (sleepMs > 1) Thread.Sleep(sleepMs - 1);
            }
        }
    }

    void ICollisionEnterSink.OnCollisionEnter(ref CollisionData data) { }
    void ICollisionExitSink .OnCollisionExit (ref CollisionData data) { }
    void ITriggerEnterSink  .OnTriggerEnter  (ref TriggerData   data) { }
    void ITriggerExitSink   .OnTriggerExit   (ref TriggerData   data) { }
}
