using System;
using AetherNet;
using AetherNet.Collision;
using Xunit;

namespace AetherNet.Tests;

public sealed class AetherEventBusTests
{
    [Fact]
    public void Raise_InvokesSubscribedHandler()
    {
        var bus = new AetherEventBus();
        int received = -1;
        bus.Subscribe(0, id => received = id);
        bus.Raise(0, 42);
        Assert.Equal(42, received);
    }

    [Fact]
    public void Raise_InvokesMultipleHandlers()
    {
        var bus = new AetherEventBus();
        int count = 0;
        bus.Subscribe(1, _ => count++);
        bus.Subscribe(1, _ => count++);
        bus.Raise(1, 0);
        Assert.Equal(2, count);
    }

    [Fact]
    public void Unsubscribe_PreventsInvocation()
    {
        var bus = new AetherEventBus();
        int count = 0;
        Action<int> handler = _ => count++;
        bus.Subscribe(0, handler);
        bus.Unsubscribe(0, handler);
        bus.Raise(0, 0);
        Assert.Equal(0, count);
    }

    [Fact]
    public void Unsubscribe_UnregisteredHandler_IsNoOp()
    {
        var bus = new AetherEventBus();
        bus.Unsubscribe(0, _ => { }); // should not throw
    }

    [Fact]
    public void Subscribe_OutOfRange_EventType_IsIgnored()
    {
        var bus = new AetherEventBus();
        int count = 0;
        bus.Subscribe(AetherEventBus.MaxEventTypes, _ => count++);
        bus.Raise(AetherEventBus.MaxEventTypes, 0);
        Assert.Equal(0, count);
    }

    [Fact]
    public void Raise_OutOfRange_EventType_DoesNotThrow()
    {
        var bus = new AetherEventBus();
        bus.Raise(AetherEventBus.MaxEventTypes + 1, 0);
    }

    [Fact]
    public void Raise_UnregisteredEventType_DoesNothing()
    {
        var bus = new AetherEventBus();
        bus.Raise(5, 10);
    }

    [Fact]
    public void Subscribe_DifferentEventTypes_AreIsolated()
    {
        var bus = new AetherEventBus();
        int t0 = 0, t1 = 0;
        bus.Subscribe(0, _ => t0++);
        bus.Subscribe(1, _ => t1++);
        bus.Raise(0, 0);
        Assert.Equal(1, t0);
        Assert.Equal(0, t1);
    }

    [Fact]
    public void Raise_PassesEntityId_ToHandler()
    {
        var bus = new AetherEventBus();
        int id = -1;
        bus.Subscribe(2, received => id = received);
        bus.Raise(2, 99);
        Assert.Equal(99, id);
    }
}

public sealed class CollisionEventQueueTests
{
    [Fact]
    public void EnqueueEnter_DrainEnter_DeliversEvent()
    {
        var queue = new CollisionEventQueue();
        var sink  = new RecordingSink();
        queue.EnqueueEnter(new CollisionData { EntityIdA = 1, EntityIdB = 2, TickNumber = 5 });
        queue.DrainEnter(sink);
        Assert.Single(sink.Enters);
        Assert.Equal(1, sink.Enters[0].EntityIdA);
        Assert.Equal(2, sink.Enters[0].EntityIdB);
    }

    [Fact]
    public void EnqueueExit_DrainExit_DeliversEvent()
    {
        var queue = new CollisionEventQueue();
        var sink  = new RecordingSink();
        queue.EnqueueExit(new CollisionData { EntityIdA = 3, EntityIdB = 4, TickNumber = 1 });
        queue.DrainExit(sink);
        Assert.Single(sink.Exits);
        Assert.Equal(3, sink.Exits[0].EntityIdA);
    }

    [Fact]
    public void EnqueueTriggerEnter_DrainTriggerEnter_DeliversEvent()
    {
        var queue = new CollisionEventQueue();
        var sink  = new RecordingSink();
        queue.EnqueueTriggerEnter(new TriggerData { TriggerEntityId = 10, OtherEntityId = 20, TickNumber = 2 });
        queue.DrainTriggerEnter(sink);
        Assert.Single(sink.TrigEnters);
        Assert.Equal(10, sink.TrigEnters[0].TriggerEntityId);
        Assert.Equal(20, sink.TrigEnters[0].OtherEntityId);
    }

    [Fact]
    public void EnqueueTriggerExit_DrainTriggerExit_DeliversEvent()
    {
        var queue = new CollisionEventQueue();
        var sink  = new RecordingSink();
        queue.EnqueueTriggerExit(new TriggerData { TriggerEntityId = 5, OtherEntityId = 6, TickNumber = 3 });
        queue.DrainTriggerExit(sink);
        Assert.Single(sink.TrigExits);
        Assert.Equal(5, sink.TrigExits[0].TriggerEntityId);
    }

    [Fact]
    public void DrainAll_DeliversAllEventTypes()
    {
        var queue = new CollisionEventQueue();
        var sink  = new RecordingSink();
        queue.EnqueueEnter(new CollisionData { EntityIdA = 1, EntityIdB = 2 });
        queue.EnqueueExit(new CollisionData { EntityIdA = 3, EntityIdB = 4 });
        queue.EnqueueTriggerEnter(new TriggerData { TriggerEntityId = 5, OtherEntityId = 6 });
        queue.EnqueueTriggerExit(new TriggerData { TriggerEntityId = 7, OtherEntityId = 8 });
        queue.DrainAll(sink);
        Assert.Single(sink.Enters);
        Assert.Single(sink.Exits);
        Assert.Single(sink.TrigEnters);
        Assert.Single(sink.TrigExits);
    }

    [Fact]
    public void Clear_PurgesAllQueues()
    {
        var queue = new CollisionEventQueue();
        var sink  = new RecordingSink();
        queue.EnqueueEnter(new CollisionData { EntityIdA = 1, EntityIdB = 2 });
        queue.EnqueueTriggerEnter(new TriggerData { TriggerEntityId = 3, OtherEntityId = 4 });
        queue.Clear();
        queue.DrainAll(sink);
        Assert.Empty(sink.Enters);
        Assert.Empty(sink.TrigEnters);
    }

    [Fact]
    public void CircularBuffer_OverCapacity_WrapsCorrectly()
    {
        var queue = new CollisionEventQueue(capacity: 2);
        var sink  = new RecordingSink();
        queue.EnqueueEnter(new CollisionData { EntityIdA = 1, EntityIdB = 0 });
        queue.EnqueueEnter(new CollisionData { EntityIdA = 2, EntityIdB = 0 });
        queue.EnqueueEnter(new CollisionData { EntityIdA = 3, EntityIdB = 0 }); // overflows
        queue.DrainEnter(sink);
        Assert.Equal(2, sink.Enters.Count);
    }

    [Fact]
    public void MultipleEnqueueDrainCycles_WorkCorrectly()
    {
        var queue = new CollisionEventQueue();
        var sink  = new RecordingSink();
        queue.EnqueueEnter(new CollisionData { EntityIdA = 1, EntityIdB = 0 });
        queue.DrainEnter(sink);
        Assert.Single(sink.Enters);

        sink.Enters.Clear();
        queue.EnqueueEnter(new CollisionData { EntityIdA = 2, EntityIdB = 0 });
        queue.DrainEnter(sink);
        Assert.Single(sink.Enters);
        Assert.Equal(2, sink.Enters[0].EntityIdA);
    }
}

public sealed class ContactTrackerExtendedTests
{
    [Fact]
    public void TryAddTrigger_FirstTime_IsNew()
    {
        var tracker = new ContactTracker(256);
        bool result = tracker.TryAddTrigger(0, 1, 1, out bool isNew);
        Assert.True(result && isNew);
    }

    [Fact]
    public void TryAddTrigger_SecondTime_NotNew()
    {
        var tracker = new ContactTracker(256);
        tracker.TryAddTrigger(0, 1, 1, out _);
        bool result = tracker.TryAddTrigger(0, 1, 2, out bool isNew);
        Assert.True(result && !isNew);
    }

    [Fact]
    public void RemoveTrigger_AllowsReAdd()
    {
        var tracker = new ContactTracker(256);
        tracker.TryAddTrigger(0, 1, 1, out _);
        bool removed    = tracker.RemoveTrigger(0, 1);
        bool addedAgain = tracker.TryAddTrigger(0, 1, 3, out bool isNew);
        Assert.True(removed);
        Assert.True(addedAgain && isNew);
    }

    [Fact]
    public void RemoveTrigger_WhenNotPresent_ReturnsFalse()
    {
        var tracker = new ContactTracker(256);
        Assert.False(tracker.RemoveTrigger(5, 6));
    }

    [Fact]
    public void TryAddNew_CommutativeKey_TreatedAsSamePair()
    {
        var tracker = new ContactTracker(256);
        tracker.TryAddNew(0, 1, 1, out _);
        bool result = tracker.TryAddNew(1, 0, 2, out bool isNew);
        Assert.True(result && !isNew);
    }
}
