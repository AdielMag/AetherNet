using System.Numerics;
using AetherNet;
using AetherNet.Network;
using Xunit;

namespace AetherNet.Tests;

public sealed class SnapshotBufferTests
{
    private static EntityState[] MakeStates(int count, float offset = 0f)
    {
        var arr = new EntityState[count];
        for (int i = 0; i < count; i++)
            arr[i] = new EntityState { EntityId = i, IsAwake = true, Transform = new TransformState { Position = new Vector2(i + offset, 0f) } };
        return arr;
    }

    [Fact]
    public void Latest_ReturnsNull_WhenEmpty()
    {
        var buf = new SnapshotBuffer(4);
        Assert.Null(buf.Latest);
    }

    [Fact]
    public void Write_ThenLatest_ReturnsWrittenSnapshot()
    {
        var buf    = new SnapshotBuffer(4);
        var states = MakeStates(3);
        buf.Write(tick: 1, timestamp: 0.1f, states, 3);

        var latest = buf.Latest;
        Assert.NotNull(latest);
        Assert.Equal(1u, latest!.TickNumber);
        Assert.Equal(3,  latest.EntityCount);
    }

    [Fact]
    public void TryGetBracketing_ReturnsFalse_WithFewerThanTwoSnapshots()
    {
        var buf = new SnapshotBuffer(4);
        buf.Write(1, 0.1f, MakeStates(1), 1);
        Assert.False(buf.TryGetBracketing(0.1f, out _, out _));
    }

    [Fact]
    public void TryGetBracketing_ReturnsCorrectPair()
    {
        var buf = new SnapshotBuffer(8);
        buf.Write(1, 0.0f, MakeStates(2, 0f), 2);
        buf.Write(2, 0.1f, MakeStates(2, 1f), 2);
        buf.Write(3, 0.2f, MakeStates(2, 2f), 2);

        bool found = buf.TryGetBracketing(0.05f, out var before, out var after);
        Assert.True(found);
        Assert.Equal(0.0f, before!.Timestamp);
        Assert.Equal(0.1f, after!.Timestamp);
    }

    [Fact]
    public void Write_OverCapacity_EvictsOldest()
    {
        var buf = new SnapshotBuffer(slotCount: 2);
        buf.Write(1, 0.1f, MakeStates(1), 1);
        buf.Write(2, 0.2f, MakeStates(1), 1);
        buf.Write(3, 0.3f, MakeStates(1), 1); // evicts tick 1

        var latest = buf.Latest;
        Assert.Equal(3u, latest!.TickNumber);
        // Bracketing from tick 1 should no longer be findable
        Assert.False(buf.TryGetBracketing(0.05f, out _, out _));
    }
}

public sealed class StateInterpolatorTests
{
    private static EntityState[] TwoEntities(float x0, float x1) =>
    [
        new EntityState { EntityId = 0, IsAwake = true, Transform = new TransformState { Position = new Vector2(x0, 0f) } },
        new EntityState { EntityId = 1, IsAwake = true, Transform = new TransformState { Position = new Vector2(x1, 0f) } },
    ];

    [Fact]
    public void Sample_ReturnsZero_WhenNoSnapshotsReceived()
    {
        var interp = new StateInterpolator(renderDelaySeconds: 0.1f);
        var output = new EntityState[16];
        Assert.Equal(0, interp.Sample(1f, output));
    }

    [Fact]
    public void Sample_ReturnsLatest_WhenBracketingNotPossible()
    {
        var interp = new StateInterpolator(renderDelaySeconds: 0f);
        var output = new EntityState[16];

        interp.ReceiveSnapshot(1, 0.0f, TwoEntities(0f, 10f), 2);
        // Only one snapshot — bracketing impossible, should return latest
        int count = interp.Sample(0.5f, output);
        Assert.Equal(2, count);
    }

    [Fact]
    public void Sample_InterpolatesPosition_Midpoint()
    {
        var interp = new StateInterpolator(renderDelaySeconds: 0f);
        var output = new EntityState[16];

        interp.ReceiveSnapshot(1, 0.0f, TwoEntities(0f, 0f),  2);
        interp.ReceiveSnapshot(2, 1.0f, TwoEntities(10f, 10f), 2);

        // renderTime = serverTime - delay = 0.5 - 0 = 0.5 → alpha = 0.5
        int count = interp.Sample(0.5f, output);
        Assert.Equal(2, count);
        Assert.True(System.Math.Abs(output[0].Transform.Position.X - 5f) < 0.01f,
            $"Expected x≈5, got {output[0].Transform.Position.X}");
    }

    [Fact]
    public void Sample_ClampsAlpha_WhenOutsideBracket()
    {
        var interp = new StateInterpolator(renderDelaySeconds: 0f);
        var output = new EntityState[16];

        interp.ReceiveSnapshot(1, 0.0f, TwoEntities(0f, 0f),  2);
        interp.ReceiveSnapshot(2, 1.0f, TwoEntities(10f, 10f), 2);

        // renderTime well beyond the after-snapshot — alpha clamped to 1.0
        int count = interp.Sample(10f, output);
        Assert.Equal(2, count);
    }
}

public sealed class TickAcknowledgerTests
{
    [Fact]
    public void Acknowledge_ThenIsAcknowledged_ReturnsTrue()
    {
        var ack = new TickAcknowledger(4);
        ack.Acknowledge(0, 5u);
        Assert.True(ack.IsAcknowledged(0, 5u));
    }

    [Fact]
    public void IsAcknowledged_ReturnsFalse_ForUnacknowledgedTick()
    {
        var ack = new TickAcknowledger(4);
        ack.Acknowledge(0, 5u);
        Assert.False(ack.IsAcknowledged(0, 6u));
    }

    [Fact]
    public void GetAcknowledgedUpTo_ReturnsContiguousRun()
    {
        var ack = new TickAcknowledger(4);
        ack.Acknowledge(0, 0u);
        ack.Acknowledge(0, 1u);
        ack.Acknowledge(0, 2u);
        // Gap at 3
        ack.Acknowledge(0, 4u);
        Assert.Equal(2u, ack.GetAcknowledgedUpTo(0));
    }

    [Fact]
    public void Reset_ClearsAcknowledgements()
    {
        var ack = new TickAcknowledger(4);
        ack.Acknowledge(0, 3u);
        ack.Reset(0);
        Assert.False(ack.IsAcknowledged(0, 3u));
    }

    [Fact]
    public void OutOfRange_ConnectionId_IsIgnored()
    {
        var ack = new TickAcknowledger(maxConnections: 2);
        ack.Acknowledge(99, 1u); // should not throw
        Assert.False(ack.IsAcknowledged(99, 1u));
        Assert.Equal(0u, ack.GetAcknowledgedUpTo(99));
    }

    [Fact]
    public void Acknowledge_SlidingWindow_HandlesHighTicks()
    {
        var ack = new TickAcknowledger(4);
        // Ack a tick far ahead to force window slide
        ack.Acknowledge(0, 100u);
        Assert.True(ack.IsAcknowledged(0, 100u));
    }
}
