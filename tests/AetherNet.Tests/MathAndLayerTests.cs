using System;
using System.Numerics;
using AetherNet;
using AetherNet.Collision;
using Xunit;

namespace AetherNet.Tests;

public sealed class MathExtensionsTests
{
    private const float Epsilon = 0.0001f;

    [Fact]
    public void ToSimulation_DividesBy100()
    {
        var world = new Vector2(100f, 200f);
        var sim   = MathExtensions.ToSimulation(in world);
        Assert.Equal(1f,  sim.X, precision: 4);
        Assert.Equal(2f,  sim.Y, precision: 4);
    }

    [Fact]
    public void ToWorld_MultipliesBy100()
    {
        var sim   = new Vector2(1f, 2f);
        var world = MathExtensions.ToWorld(in sim);
        Assert.Equal(100f, world.X, precision: 4);
        Assert.Equal(200f, world.Y, precision: 4);
    }

    [Fact]
    public void SimWorld_RoundTrips()
    {
        var original = new Vector2(123f, 456f);
        var roundtrip = MathExtensions.ToWorld(MathExtensions.ToSimulation(in original));
        Assert.True(MathF.Abs(original.X - roundtrip.X) < Epsilon);
        Assert.True(MathF.Abs(original.Y - roundtrip.Y) < Epsilon);
    }

    [Fact]
    public void ToSimAngle_ConvertsDegreesToRadians_Negated()
    {
        float result = MathExtensions.ToSimAngle(90f);
        Assert.True(MathF.Abs(result - (-MathF.PI / 2f)) < Epsilon);
    }

    [Fact]
    public void ToWorldAngle_ConvertsRadiansToDegrees_Negated()
    {
        float result = MathExtensions.ToWorldAngle(-MathF.PI / 2f);
        Assert.True(MathF.Abs(result - 90f) < Epsilon);
    }

    [Fact]
    public void Lerp_AtZero_ReturnsA()   => Assert.Equal(5f,  MathExtensions.Lerp(5f, 10f, 0f));
    [Fact]
    public void Lerp_AtOne_ReturnsB()    => Assert.Equal(10f, MathExtensions.Lerp(5f, 10f, 1f));
    [Fact]
    public void Lerp_AtHalf_ReturnsMid() => Assert.Equal(7.5f, MathExtensions.Lerp(5f, 10f, 0.5f));

    [Fact]
    public void LerpAngle_WrapsCorrectly()
    {
        // Lerping from 350° to 10° at t=0.5 should give ~0° (shortest path)
        float result = MathExtensions.LerpAngle(350f, 10f, 0.5f);
        Assert.True(MathF.Abs(result - 0f) < 1f, $"Expected ~0°, got {result}");
    }
}

public sealed class PhysicsLayersTests
{
    [Fact]
    public void Bit_ReturnsCorrectPowerOfTwo()
    {
        Assert.Equal(1,  PhysicsLayers.Bit(0));
        Assert.Equal(2,  PhysicsLayers.Bit(1));
        Assert.Equal(4,  PhysicsLayers.Bit(2));
        Assert.Equal(16, PhysicsLayers.Bit(4));
    }

    [Fact]
    public void MaskFor_DefaultLayer_ReturnsAll()
        => Assert.Equal(PhysicsLayers.All, PhysicsLayers.MaskFor(PhysicsLayers.Default));

    [Fact]
    public void MaskFor_PlayerLayer_ExcludesProjectile()
    {
        int mask = PhysicsLayers.MaskFor(PhysicsLayers.Player);
        Assert.Equal(0, mask & PhysicsLayers.Bit(PhysicsLayers.Projectile));
    }

    [Fact]
    public void MaskFor_TriggerLayer_OnlySeesPlayer()
    {
        int mask = PhysicsLayers.MaskFor(PhysicsLayers.Trigger);
        Assert.NotEqual(0, mask & PhysicsLayers.Bit(PhysicsLayers.Player));
        Assert.Equal(0, mask & PhysicsLayers.Bit(PhysicsLayers.Environment));
    }

    [Fact]
    public void MaskFor_UnknownLayer_ReturnsAll()
        => Assert.Equal(PhysicsLayers.All, PhysicsLayers.MaskFor(999));
}

public sealed class CollisionFilterTests
{
    [Fact]
    public void Default_HasCorrectCategoryAndMask()
    {
        var f = CollisionFilter.Default;
        Assert.Equal(0x0001, f.CategoryBits);
        Assert.Equal(0xFFFF, f.MaskBits);
        Assert.Equal(0,      f.GroupIndex);
    }

    [Fact]
    public void FromLayer_SetsCorrectCategory()
    {
        var f = CollisionFilter.FromLayer(PhysicsLayers.Player);
        Assert.Equal((ushort)(1 << PhysicsLayers.Player), f.CategoryBits);
    }

    [Fact]
    public void FromLayer_OverrideMask_IsApplied()
    {
        var f = CollisionFilter.FromLayer(PhysicsLayers.Player, overrideMask: 0x00FF);
        Assert.Equal(0x00FF, f.MaskBits);
    }

    [Fact]
    public void FromLayer_NoOverride_UsesMaskForLayer()
    {
        var f = CollisionFilter.FromLayer(PhysicsLayers.Trigger);
        Assert.Equal((ushort)PhysicsLayers.MaskFor(PhysicsLayers.Trigger), f.MaskBits);
    }
}
