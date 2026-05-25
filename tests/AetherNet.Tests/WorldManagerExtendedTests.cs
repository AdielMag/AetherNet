using System;
using System.Numerics;
using System.Runtime.InteropServices;
using AetherNet;
using AetherNet.Collision;
using AetherNet.Network;
using AetherNet.Queries;
using nkast.Aether.Physics2D.Dynamics;
using Xunit;
using AVec2 = nkast.Aether.Physics2D.Common.Vector2;

namespace AetherNet.Tests;

internal sealed class TestNetworkProvider : INetworkStateProvider
{
    public uint? LastTick;
    public int   LastCount;
    public void OnTickComplete(uint tick, EntityState[] states, int count) { LastTick = tick; LastCount = count; }
    public void ApplySnapshot(uint tick, EntityState[] states, int count) { }
}

public sealed class WorldManagerExtendedTests
{
    private static PhysicsWorldManager ZeroGravityWorld()
        => new PhysicsWorldManager(WorldConfig.Default with { Gravity = Vector2.Zero });

    private static Body AddDynamic(PhysicsWorldManager world, int id, Vector2 pos = default)
    {
        var def = new BodyDef { BodyType = BodyType.Dynamic, Position = pos };
        return world.CreateBody(in def, id);
    }

    private static Body AddDynamicWithShape(PhysicsWorldManager world, int id, Vector2 pos = default)
    {
        var def = new BodyDef { BodyType = BodyType.Dynamic, Position = pos };
        var body = world.CreateBody(in def, id);
        body.CreateCircle(0.5f, 1f, new AVec2(0f, 0f));
        return body;
    }

    [Fact]
    public void ActiveBodyCount_StartsAtZero()
    {
        var w = ZeroGravityWorld();
        Assert.Equal(0, w.ActiveBodyCount);
    }

    [Fact]
    public void CreateBody_IncrementsActiveBodyCount()
    {
        var w = ZeroGravityWorld();
        AddDynamic(w, 0);
        Assert.Equal(1, w.ActiveBodyCount);
    }

    [Fact]
    public void DestroyBody_DecrementsActiveBodyCount()
    {
        var w = ZeroGravityWorld();
        AddDynamic(w, 0);
        w.DestroyBody(0);
        Assert.Equal(0, w.ActiveBodyCount);
    }

    [Fact]
    public void DestroyBody_NonExistentId_IsNoOp()
    {
        var w = ZeroGravityWorld();
        w.DestroyBody(5);
    }

    [Fact]
    public void CreateBody_OutOfRange_Throws()
    {
        var w = ZeroGravityWorld();
        var def = BodyDef.Dynamic;
        Assert.Throws<ArgumentOutOfRangeException>(() => w.CreateBody(in def, -1));
    }

    [Fact]
    public void CreateBody_DuplicateId_Throws()
    {
        var w = ZeroGravityWorld();
        var def = BodyDef.Dynamic;
        w.CreateBody(in def, 0);
        Assert.Throws<InvalidOperationException>(() => w.CreateBody(in def, 0));
    }

    [Fact]
    public void TickNumber_IncrementsOnAdvance()
    {
        var w = ZeroGravityWorld();
        uint before = w.TickNumber;
        w.Advance(SimulationConstants.FixedTimestep); // exactly one tick
        Assert.True(w.TickNumber > before);
    }

    [Fact]
    public void InterpolationAlpha_BetweenZeroAndOne_AfterPartialAdvance()
    {
        var w = ZeroGravityWorld();
        w.Advance(0.005f);
        float alpha = w.InterpolationAlpha;
        Assert.True(alpha >= 0f && alpha <= 1f);
    }

    [Fact]
    public void SetPosition_MovesBody()
    {
        var w = ZeroGravityWorld();
        AddDynamic(w, 0);
        var newPos = new Vector2(5f, 3f);
        w.SetPosition(0, in newPos);
        var state = w.GetBodyState(0);
        Assert.True(MathF.Abs(state.Position.X - 5f) < 0.001f);
        Assert.True(MathF.Abs(state.Position.Y - 3f) < 0.001f);
    }

    [Fact]
    public void SetPosition_NonExistentEntity_IsNoOp()
    {
        var w = ZeroGravityWorld();
        var pos = new Vector2(1f, 1f);
        w.SetPosition(99, in pos);
    }

    [Fact]
    public void SetAngularVelocity_UpdatesVelocity()
    {
        var w = ZeroGravityWorld();
        AddDynamic(w, 0);
        w.SetAngularVelocity(0, 3.14f);
        Assert.True(MathF.Abs(w.GetAngularVelocity(0) - 3.14f) < 0.001f);
    }

    [Fact]
    public void SetSleepState_AndIsSleeping()
    {
        var w = ZeroGravityWorld();
        AddDynamic(w, 0);
        w.SetSleepState(0, true);
        Assert.True(w.IsSleeping(0));
        w.SetSleepState(0, false);
        Assert.False(w.IsSleeping(0));
    }

    [Fact]
    public void SetSleepState_NonExistentEntity_IsNoOp()
    {
        var w = ZeroGravityWorld();
        w.SetSleepState(99, true);
    }

    [Fact]
    public void IsSleeping_NonExistentEntity_ReturnsFalse()
    {
        var w = ZeroGravityWorld();
        Assert.False(w.IsSleeping(99));
    }

    [Fact]
    public void ResetDynamics_ZerosVelocity()
    {
        var w = ZeroGravityWorld();
        AddDynamic(w, 0);
        var impulse = new Vector2(10f, 0f);
        w.ApplyForce(0, in impulse, ForceMode.Impulse);
        w.ResetDynamics(0);
        var vel = w.GetLinearVelocity(0);
        Assert.True(MathF.Abs(vel.X) < 0.001f);
    }

    [Fact]
    public void ApplyForce_Impulse_ChangesVelocity()
    {
        var w = ZeroGravityWorld();
        AddDynamic(w, 0);
        var impulse = new Vector2(10f, 0f);
        w.ApplyForce(0, in impulse, ForceMode.Impulse);
        var vel = w.GetLinearVelocity(0);
        Assert.True(vel.X > 0f);
    }

    [Fact]
    public void ApplyForce_VelocityChange_ChangesVelocity()
    {
        var w = ZeroGravityWorld();
        AddDynamic(w, 0);
        var vc = new Vector2(5f, 0f);
        w.ApplyForce(0, in vc, ForceMode.VelocityChange);
        var vel = w.GetLinearVelocity(0);
        Assert.True(vel.X > 0f);
    }

    [Fact]
    public void ApplyForce_Acceleration_DoesNotThrow()
    {
        var w = ZeroGravityWorld();
        AddDynamic(w, 0);
        var acc = new Vector2(1f, 0f);
        w.ApplyForce(0, in acc, ForceMode.Acceleration);
    }

    [Fact]
    public void ApplyForce_Force_DoesNotThrow()
    {
        var w = ZeroGravityWorld();
        AddDynamic(w, 0);
        var force = new Vector2(1f, 0f);
        w.ApplyForce(0, in force, ForceMode.Force);
    }

    [Fact]
    public void ApplyForce_NonDynamicBody_IsNoOp()
    {
        var w = ZeroGravityWorld();
        var def = BodyDef.Static;
        w.CreateBody(in def, 0);
        var force = new Vector2(100f, 0f);
        w.ApplyForce(0, in force, ForceMode.Impulse);
    }

    [Fact]
    public void ApplyTorque_DoesNotThrow()
    {
        var w = ZeroGravityWorld();
        AddDynamic(w, 0);
        w.ApplyTorque(0, 5f);
    }

    [Fact]
    public void ApplyAngularImpulse_ChangesAngularVelocity()
    {
        var w = ZeroGravityWorld();
        AddDynamicWithShape(w, 0);
        w.ApplyAngularImpulse(0, 10f);
        Assert.True(MathF.Abs(w.GetAngularVelocity(0)) > 0f);
    }

    [Fact]
    public void ApplyForceAtPoint_DoesNotThrow()
    {
        var w = ZeroGravityWorld();
        AddDynamic(w, 0);
        var force = new Vector2(1f, 0f);
        var point = new Vector2(0f, 1f);
        w.ApplyForceAtPoint(0, in force, in point);
    }

    [Fact]
    public void GetBodyState_NonExistent_ReturnsDefault()
    {
        var w = ZeroGravityWorld();
        var state = w.GetBodyState(99);
        Assert.Equal(default(TransformState), state);
    }

    [Fact]
    public void GetMass_WithShape_ReturnsMass()
    {
        var w = ZeroGravityWorld();
        AddDynamicWithShape(w, 0);
        Assert.True(w.GetMass(0) > 0f);
    }

    [Fact]
    public void GetMass_NonExistent_ReturnsZero()
    {
        var w = ZeroGravityWorld();
        Assert.Equal(0f, w.GetMass(99));
    }

    [Fact]
    public void GetLinearVelocity_NonExistent_ReturnsZero()
    {
        var w = ZeroGravityWorld();
        Assert.Equal(Vector2.Zero, w.GetLinearVelocity(99));
    }

    [Fact]
    public void GetAngularVelocity_NonExistent_ReturnsZero()
    {
        var w = ZeroGravityWorld();
        Assert.Equal(0f, w.GetAngularVelocity(99));
    }

    [Fact]
    public void CopyStateTo_ReturnsAllBodies()
    {
        var w = ZeroGravityWorld();
        AddDynamic(w, 0);
        AddDynamic(w, 1);
        var buf = new EntityState[256];
        w.CopyStateTo(buf, out int count);
        Assert.Equal(2, count);
    }

    [Fact]
    public void SetNetworkProvider_ReceivesTickCallback()
    {
        var w = ZeroGravityWorld();
        var provider = new TestNetworkProvider();
        w.SetNetworkProvider(provider);
        w.Advance(0.016f);
        Assert.NotNull(provider.LastTick);
    }

    [Fact]
    public void SetNetworkProvider_Null_DoesNotThrow()
    {
        var w = ZeroGravityWorld();
        w.SetNetworkProvider(null);
        w.Advance(0.016f);
    }

    [Fact]
    public void Raycast_MissesBody_ReturnsZero()
    {
        var w = ZeroGravityWorld();
        AddDynamicWithShape(w, 0, new Vector2(100f, 100f));
        var queryBuf = new PhysicsQueryBuffer();
        var origin = new Vector2(0f, 0f);
        var dir = new Vector2(0f, 1f);
        w.Raycast(in origin, in dir, 5f, queryBuf);
        Assert.Equal(0, queryBuf.RaycastCount);
    }

    [Fact]
    public void Raycast_HitsBody_ReturnsHit()
    {
        var w = ZeroGravityWorld();
        // Static body at origin with a circle shape that the ray will cross
        var def = BodyDef.Static with { Position = Vector2.Zero };
        var body = w.CreateBody(in def, 0);
        body.CreateCircle(0.5f, 1f, new AVec2(0f, 0f));
        var queryBuf = new PhysicsQueryBuffer();
        var origin = new Vector2(-5f, 0f);
        var dir = new Vector2(1f, 0f);
        w.Raycast(in origin, in dir, 20f, queryBuf);
        Assert.True(queryBuf.RaycastCount > 0);
    }

    [Fact]
    public void FreezePositionY_ConstrainsBody()
    {
        var w = new PhysicsWorldManager(WorldConfig.Default);
        var def = new BodyDef
        {
            BodyType    = BodyType.Dynamic,
            Constraints = RigidbodyConstraints.FreezePositionY,
        };
        var body = w.CreateBody(in def, 0);
        body.CreateCircle(0.5f, 1f, new AVec2(0f, 0f));
        float initialY = body.Position.Y;
        w.Advance(0.5f);
        Assert.True(MathF.Abs(body.Position.Y - initialY) < 0.01f);
    }

    [Fact]
    public void FreezeRotation_InBodyDef_SetsFixedRotation()
    {
        var w = ZeroGravityWorld();
        var def = new BodyDef
        {
            BodyType    = BodyType.Dynamic,
            Constraints = RigidbodyConstraints.FreezeRotation,
        };
        var body = w.CreateBody(in def, 0);
        Assert.True(body.FixedRotation);
    }

    [Fact]
    public void DestroyBody_ThenRecreate_Works()
    {
        var w = ZeroGravityWorld();
        AddDynamic(w, 0);
        w.DestroyBody(0);
        var def = BodyDef.Dynamic;
        var body = w.CreateBody(in def, 0);
        Assert.NotNull(body);
    }

    [Fact]
    public void Events_Property_NotNull()
    {
        var w = ZeroGravityWorld();
        Assert.NotNull(w.Events);
    }
}

public sealed class BodySleepManagerTests
{
    [Fact]
    public void Update_DeactivatesDistantBody()
    {
        var aetherWorld = new World(new AVec2(0f, 0f));
        var body = aetherWorld.CreateBody(new AVec2(100f, 0f), 0f, BodyType.Dynamic);
        body.CreateCircle(0.5f, 1f, new AVec2(0f, 0f));

        var registry = new Body[256];
        registry[0] = body;
        var manager = new BodySleepManager(registry, deactivationRadius: 10f);
        var focus = new Vector2(0f, 0f);
        manager.Update(in focus);
        Assert.False(body.Enabled);
    }

    [Fact]
    public void Update_ReactivatesBodyWhenFocusMoves()
    {
        var aetherWorld = new World(new AVec2(0f, 0f));
        var body = aetherWorld.CreateBody(new AVec2(100f, 0f), 0f, BodyType.Dynamic);
        body.CreateCircle(0.5f, 1f, new AVec2(0f, 0f));

        var registry = new Body[256];
        registry[0] = body;
        var manager = new BodySleepManager(registry, deactivationRadius: 10f);

        var farFocus = new Vector2(0f, 0f);
        manager.Update(in farFocus);
        Assert.False(body.Enabled);

        var closeFocus = new Vector2(100f, 0f);
        manager.Update(in closeFocus);
        Assert.True(body.Enabled);
    }

    [Fact]
    public void Update_NullBodyInRegistry_IsSkipped()
    {
        var registry = new Body[4];
        var manager = new BodySleepManager(registry, deactivationRadius: 10f);
        var focus = new Vector2(0f, 0f);
        manager.Update(in focus);
    }

    [Fact]
    public void Update_StaticBody_IsNotDeactivated()
    {
        var aetherWorld = new World(new AVec2(0f, 0f));
        var body = aetherWorld.CreateBody(new AVec2(100f, 0f), 0f, BodyType.Static);

        var registry = new Body[256];
        registry[0] = body;
        var manager = new BodySleepManager(registry, deactivationRadius: 10f);
        var focus = new Vector2(0f, 0f);
        manager.Update(in focus);
        Assert.True(body.Enabled);
    }
}

public sealed class AetherInteropTests
{
    [Fact]
    public void ToAether_FromAether_RoundTrip()
    {
        var original = new Vector2(3.14f, -2.71f);
        var aether = AetherInterop.ToAether(in original);
        var roundtrip = AetherInterop.FromAether(aether);
        Assert.True(MathF.Abs(roundtrip.X - original.X) < 0.0001f);
        Assert.True(MathF.Abs(roundtrip.Y - original.Y) < 0.0001f);
    }

    [Fact]
    public void ToAether_Zero_ReturnsZero()
    {
        var zero = Vector2.Zero;
        var aether = AetherInterop.ToAether(in zero);
        Assert.Equal(0f, aether.X);
        Assert.Equal(0f, aether.Y);
    }

    [Fact]
    public void FromAether_Zero_ReturnsZero()
    {
        var aether = new AVec2(0f, 0f);
        var result = AetherInterop.FromAether(aether);
        Assert.Equal(0f, result.X);
        Assert.Equal(0f, result.Y);
    }

    [Fact]
    public void ToAether_PreservesSign()
    {
        var v = new Vector2(-5f, 7f);
        var aether = AetherInterop.ToAether(in v);
        Assert.Equal(-5f, aether.X);
        Assert.Equal(7f, aether.Y);
    }
}

public sealed class StateSerializerTests
{
    private static readonly int EntityStateSize = Marshal.SizeOf<EntityState>();

    [Fact]
    public void PayloadSize_ReturnsExpectedSize()
    {
        int size = StateSerializer.PayloadSize(10);
        Assert.Equal(
            sizeof(uint) + sizeof(float) + sizeof(int) + sizeof(uint) + 10 * EntityStateSize,
            size);
    }

    [Fact]
    public void PayloadSize_ZeroEntities()
    {
        int size = StateSerializer.PayloadSize(0);
        Assert.Equal(sizeof(uint) + sizeof(float) + sizeof(int) + sizeof(uint), size);
    }

    [Fact]
    public void Serialize_Deserialize_RoundTrip()
    {
        var states = new EntityState[]
        {
            new EntityState { EntityId = 1, IsAwake = true,  Transform = new TransformState { Position = new Vector2(1f, 2f), Angle = 0.5f } },
            new EntityState { EntityId = 2, IsAwake = false, Transform = new TransformState { Position = new Vector2(3f, 4f) } },
        };
        byte[] buffer = new byte[states.Length * EntityStateSize + 64];
        int written = StateSerializer.Serialize(states, 2, buffer, 0);
        Assert.Equal(2 * EntityStateSize, written);

        var output = new EntityState[2];
        int count = StateSerializer.Deserialize(buffer, 0, written, output);
        Assert.Equal(2, count);
        Assert.Equal(1, output[0].EntityId);
        Assert.Equal(2, output[1].EntityId);
        Assert.True(MathF.Abs(output[0].Transform.Position.X - 1f) < 0.0001f);
    }

    [Fact]
    public void Serialize_BufferTooSmall_Throws()
    {
        var states = new EntityState[10];
        byte[] tinyBuffer = new byte[1];
        Assert.Throws<ArgumentException>(() => StateSerializer.Serialize(states, 10, tinyBuffer, 0));
    }

    [Fact]
    public void Deserialize_DestinationTooSmall_Throws()
    {
        var states = new EntityState[5];
        byte[] buffer = new byte[5 * EntityStateSize];
        StateSerializer.Serialize(states, 5, buffer, 0);
        var dest = new EntityState[2];
        Assert.Throws<ArgumentException>(() => StateSerializer.Deserialize(buffer, 0, 5 * EntityStateSize, dest));
    }

    [Fact]
    public void Serialize_WithOffset_Works()
    {
        var states = new EntityState[] { new EntityState { EntityId = 7 } };
        byte[] buffer = new byte[EntityStateSize + 16];
        int written = StateSerializer.Serialize(states, 1, buffer, 8);
        var dest = new EntityState[1];
        StateSerializer.Deserialize(buffer, 8, written, dest);
        Assert.Equal(7, dest[0].EntityId);
    }
}

public sealed class StateSnapshotTests
{
    [Fact]
    public void StateSnapshot_DefaultValues()
    {
        var snapshot = default(StateSnapshot);
        Assert.Equal(0u, snapshot.TickNumber);
        Assert.Equal(0f, snapshot.SimulationTime);
        Assert.Equal(0, snapshot.EntityCount);
        Assert.Equal(0u, snapshot.DeterminismHash);
    }

    [Fact]
    public void StateSnapshot_CanSetFields()
    {
        var snapshot = new StateSnapshot
        {
            TickNumber      = 42,
            SimulationTime  = 1.5f,
            EntityCount     = 10,
            DeterminismHash = 0xDEADBEEF,
        };
        Assert.Equal(42u, snapshot.TickNumber);
        Assert.Equal(1.5f, snapshot.SimulationTime);
        Assert.Equal(10, snapshot.EntityCount);
        Assert.Equal(0xDEADBEEF, snapshot.DeterminismHash);
    }
}
