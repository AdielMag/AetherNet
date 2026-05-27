using System;
using nkast.Aether.Physics2D.Dynamics;
using nkast.Aether.Physics2D.Dynamics.Contacts;
using AetherNet.Collision;
using AetherNet.Network;
using AetherNet.Queries;
using AVec2 = nkast.Aether.Physics2D.Common.Vector2;
using SNV2  = System.Numerics.Vector2;

namespace AetherNet;

public sealed class PhysicsWorldManager
{
    private readonly World _world;
    private readonly Body[]                 _bodyRegistry;
    private readonly EntityToken[]          _entityTokens;
    private readonly EntityState[]          _stateBuffer;
    private readonly RigidbodyConstraints[] _constraints;
    private readonly SNV2[]                 _frozenPositions;
    private readonly int[] _frozenIds;
    private int            _frozenCount;
    private int  _activeBodyCount;
    private uint _tickNumber;
    private float _accumulator;
    private readonly CollisionEventQueue _events;
    private readonly ContactTracker      _contactTracker;
    private readonly OnCollisionEventHandler  _onCollisionHandler;
    private readonly OnSeparationEventHandler _onSeparationHandler;
    private readonly PhysicsQueryBuffer _queryBuffer;
    private readonly RayCastReportFixtureDelegate _rayCastCallback;
    private PhysicsQueryBuffer? _activeQueryBuffer;
    private INetworkStateProvider? _networkProvider;
    private readonly int _maxBodies;

    public PhysicsWorldManager(in WorldConfig config)
    {
        _maxBodies = config.MaxBodies > 0 ? config.MaxBodies : SimulationConstants.MaxBodies;
        _world = new World(AetherInterop.ToAether(config.Gravity));
        _bodyRegistry    = new Body[_maxBodies];
        _entityTokens    = new EntityToken[_maxBodies];
        _stateBuffer     = new EntityState[_maxBodies];
        _constraints     = new RigidbodyConstraints[_maxBodies];
        _frozenPositions = new SNV2[_maxBodies];
        _frozenIds       = new int[_maxBodies];
        _events         = new CollisionEventQueue();
        _contactTracker = new ContactTracker(SimulationConstants.MaxContacts);
        _queryBuffer    = new PhysicsQueryBuffer();
        _onCollisionHandler  = HandleCollision;
        _onSeparationHandler = HandleSeparation;
        _rayCastCallback     = RayCastCallback;
    }

    public float InterpolationAlpha => _accumulator / SimulationConstants.FixedTimestep;
    public uint  TickNumber      => _tickNumber;
    public int   ActiveBodyCount => _activeBodyCount;
    public CollisionEventQueue Events => _events;

    public void Advance(float deltaTime)
    {
        _accumulator += deltaTime;
        while (_accumulator >= SimulationConstants.FixedTimestep)
        {
            _world.Step(SimulationConstants.FixedTimestep);
            ApplyFreezeConstraints();
            _tickNumber++;
            _accumulator -= SimulationConstants.FixedTimestep;
        }
        if (_networkProvider != null)
        {
            CopyStateTo(_stateBuffer, out int count);
            _networkProvider.OnTickComplete(_tickNumber, _stateBuffer, count);
        }
    }

    public Body CreateBody(in BodyDef def, int entityId)
    {
        if ((uint)entityId >= (uint)_maxBodies)
            throw new ArgumentOutOfRangeException(nameof(entityId));
        if (_bodyRegistry[entityId] != null)
            throw new InvalidOperationException($"Entity {entityId} is already registered.");

        Body body = _world.CreateBody(AetherInterop.ToAether(def.Position), def.Angle, def.BodyType);
        body.LinearDamping  = def.LinearDamping;
        body.AngularDamping = def.AngularDamping;
        body.FixedRotation  = def.FixedRotation || (def.Constraints & RigidbodyConstraints.FreezeRotation) != 0;

        var token = new EntityToken { EntityId = entityId };
        _entityTokens[entityId] = token;
        body.Tag                = token;
        _bodyRegistry[entityId]    = body;
        _constraints[entityId]     = def.Constraints;
        _frozenPositions[entityId] = def.Position;

        if ((def.Constraints & (RigidbodyConstraints.FreezePositionX | RigidbodyConstraints.FreezePositionY)) != 0)
            _frozenIds[_frozenCount++] = entityId;

        _activeBodyCount++;
        return body;
    }

    public void DestroyBody(int entityId)
    {
        Body? body = GetBodyOrNull(entityId);
        if (body == null) return;
        _world.Remove(body);
        _bodyRegistry[entityId]  = null!;
        _entityTokens[entityId]  = null!;
        _constraints[entityId]   = RigidbodyConstraints.None;
        _activeBodyCount--;
        for (int i = 0; i < _frozenCount; i++)
        {
            if (_frozenIds[i] == entityId) { _frozenIds[i] = _frozenIds[--_frozenCount]; break; }
        }
    }

    public void SubscribeFixtureEvents(Fixture fixture)
    {
        fixture.OnCollision  += _onCollisionHandler;
        fixture.OnSeparation += _onSeparationHandler;
    }

    public void CopyStateTo(EntityState[] destination, out int count)
    {
        count = 0;
        for (int id = 0; id < _maxBodies; id++)
        {
            Body? body = _bodyRegistry[id];
            if (body == null) continue;
            destination[count++] = new EntityState
            {
                EntityId = id, IsAwake = body.Awake,
                Transform = new TransformState
                {
                    Position        = AetherInterop.FromAether(body.Position),
                    Angle           = body.Rotation,
                    LinearVelocity  = AetherInterop.FromAether(body.LinearVelocity),
                    AngularVelocity = body.AngularVelocity,
                },
            };
        }
    }

    public TransformState GetBodyState(int entityId)
    {
        Body? body = GetBodyOrNull(entityId);
        if (body == null) return default;
        return new TransformState
        {
            Position        = AetherInterop.FromAether(body.Position),
            Angle           = body.Rotation,
            LinearVelocity  = AetherInterop.FromAether(body.LinearVelocity),
            AngularVelocity = body.AngularVelocity,
        };
    }

    public void ApplyForce(int entityId, in SNV2 force, ForceMode mode = ForceMode.Force)
    {
        Body? body = GetDynamicBodyOrNull(entityId);
        if (body == null) return;
        AVec2 f = AetherInterop.ToAether(force);
        switch (mode)
        {
            case ForceMode.Force:          body.ApplyForce(f); break;
            case ForceMode.Impulse:        body.ApplyLinearImpulse(f); break;
            case ForceMode.VelocityChange: body.ApplyLinearImpulse(new AVec2(f.X * body.Mass, f.Y * body.Mass)); break;
            case ForceMode.Acceleration:   body.ApplyForce(new AVec2(f.X * body.Mass, f.Y * body.Mass)); break;
        }
    }

    public void ApplyForceAtPoint(int entityId, in SNV2 force, in SNV2 worldPoint)
        => GetDynamicBodyOrNull(entityId)?.ApplyForce(AetherInterop.ToAether(force), AetherInterop.ToAether(worldPoint));

    public void ApplyTorque(int entityId, float torque) => GetDynamicBodyOrNull(entityId)?.ApplyTorque(torque);
    public void ApplyAngularImpulse(int entityId, float impulse) => GetDynamicBodyOrNull(entityId)?.ApplyAngularImpulse(impulse);

    public SNV2  GetLinearVelocity(int entityId)  => AetherInterop.FromAether(GetBodyOrNull(entityId)?.LinearVelocity ?? default);
    public float GetAngularVelocity(int entityId) => GetBodyOrNull(entityId)?.AngularVelocity ?? 0f;
    public float GetMass(int entityId)            => GetBodyOrNull(entityId)?.Mass ?? 0f;

    public void SetLinearVelocity(int entityId, in SNV2 vel)  { Body? b = GetBodyOrNull(entityId); if (b != null) b.LinearVelocity = AetherInterop.ToAether(vel); }
    public void SetAngularVelocity(int entityId, float angVel){ Body? b = GetBodyOrNull(entityId); if (b != null) b.AngularVelocity = angVel; }
    public void SetPosition(int entityId, in SNV2 pos)        { Body? b = GetBodyOrNull(entityId); if (b != null) b.Position = AetherInterop.ToAether(pos); }
    public bool IsSleeping(int entityId) { var b = GetBodyOrNull(entityId); return b != null && !b.Awake; }
    public void SetSleepState(int entityId, bool sleep)       { Body? b = GetBodyOrNull(entityId); if (b == null) return; b.Awake = !sleep; }
    public void ResetDynamics(int entityId) => GetBodyOrNull(entityId)?.ResetDynamics();

    public void Raycast(in SNV2 origin, in SNV2 direction, float distance, PhysicsQueryBuffer buffer, int layerMask = -1)
    {
        buffer.ClearRaycast();
        _activeQueryBuffer = buffer;
        SNV2 end = origin + direction * distance;
        _world.RayCast(_rayCastCallback, AetherInterop.ToAether(origin), AetherInterop.ToAether(end));
        _activeQueryBuffer = null;
    }

    public void SetNetworkProvider(INetworkStateProvider? provider) => _networkProvider = provider;

    private bool HandleCollision(Fixture sender, Fixture other, Contact contact)
    {
        if (sender.Body.Tag is not EntityToken tokenA) return true;
        if (other.Body.Tag  is not EntityToken tokenB) return true;
        int idA = tokenA.EntityId, idB = tokenB.EntityId;
        if (sender.IsSensor || other.IsSensor)
        {
            if (_contactTracker.TryAddTrigger(idA, idB, _tickNumber, out bool isNew) && isNew)
                _events.EnqueueTriggerEnter(new TriggerData { TriggerEntityId = sender.IsSensor ? idA : idB, OtherEntityId = sender.IsSensor ? idB : idA, TickNumber = _tickNumber });
        }
        else
        {
            if (_contactTracker.TryAddNew(idA, idB, _tickNumber, out bool isNew) && isNew)
            {
                SNV2 contactPoint = default, contactNormal = default;
                if (contact.Manifold.PointCount > 0)
                {
                    contact.GetWorldManifold(out AVec2 normal, out var points);
                    contactNormal = AetherInterop.FromAether(normal);
                    contactPoint  = AetherInterop.FromAether(points[0]);
                }
                _events.EnqueueEnter(new CollisionData { EntityIdA = idA, EntityIdB = idB, ContactPoint = contactPoint, ContactNormal = contactNormal, TickNumber = _tickNumber });
            }
        }
        return true;
    }

    private void HandleSeparation(Fixture sender, Fixture other, Contact contact)
    {
        if (sender.Body.Tag is not EntityToken tokenA) return;
        if (other.Body.Tag  is not EntityToken tokenB) return;
        int idA = tokenA.EntityId, idB = tokenB.EntityId;
        if (sender.IsSensor || other.IsSensor)
        {
            if (_contactTracker.RemoveTrigger(idA, idB))
                _events.EnqueueTriggerExit(new TriggerData { TriggerEntityId = sender.IsSensor ? idA : idB, OtherEntityId = sender.IsSensor ? idB : idA, TickNumber = _tickNumber });
        }
        else
        {
            if (_contactTracker.Remove(idA, idB))
                _events.EnqueueExit(new CollisionData { EntityIdA = idA, EntityIdB = idB, TickNumber = _tickNumber });
        }
    }

    private float RayCastCallback(Fixture fixture, AVec2 point, AVec2 normal, float fraction)
    {
        if (_activeQueryBuffer == null) return -1f;
        if (_activeQueryBuffer.RaycastCount >= _activeQueryBuffer.RaycastResults.Length) return 0f;
        int entityId = (fixture.Body.Tag as EntityToken)?.EntityId ?? -1;
        _activeQueryBuffer.RaycastResults[_activeQueryBuffer.RaycastCount++] = new RaycastHit
        {
            EntityId = entityId, FixtureIndex = 0, Point = AetherInterop.FromAether(point),
            Normal = AetherInterop.FromAether(normal), Fraction = fraction, IsTrigger = fixture.IsSensor,
        };
        return 1f;
    }

    private void ApplyFreezeConstraints()
    {
        for (int i = 0; i < _frozenCount; i++)
        {
            int id = _frozenIds[i];
            Body? body = _bodyRegistry[id];
            if (body == null) continue;
            RigidbodyConstraints c = _constraints[id];
            SNV2 pos = AetherInterop.FromAether(body.Position);
            SNV2 vel = AetherInterop.FromAether(body.LinearVelocity);
            if ((c & RigidbodyConstraints.FreezePositionX) != 0) { pos.X = _frozenPositions[id].X; vel.X = 0f; }
            if ((c & RigidbodyConstraints.FreezePositionY) != 0) { pos.Y = _frozenPositions[id].Y; vel.Y = 0f; }
            body.Position = AetherInterop.ToAether(pos);
            body.LinearVelocity = AetherInterop.ToAether(vel);
        }
    }

    private Body? GetBodyOrNull(int entityId) => (uint)entityId < (uint)_maxBodies ? _bodyRegistry[entityId] : null;
    private Body? GetDynamicBodyOrNull(int entityId) { Body? b = GetBodyOrNull(entityId); return b?.BodyType == BodyType.Dynamic ? b : null; }
}
