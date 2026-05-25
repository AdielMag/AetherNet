using nkast.Aether.Physics2D.Dynamics;
using SNV2 = System.Numerics.Vector2;

namespace AetherNet;

public sealed class BodySleepManager
{
    private readonly Body[]  _registry;
    private readonly int[]   _deactivated;
    private int              _deactivatedCount;
    private readonly float   _deactivationRadiusSq;

    public BodySleepManager(Body[] bodyRegistry, float deactivationRadius = 50f)
    {
        _registry             = bodyRegistry;
        _deactivated          = new int[bodyRegistry.Length];
        _deactivationRadiusSq = deactivationRadius * deactivationRadius;
    }

    public void Update(in SNV2 focusPointSimUnits)
    {
        for (int i = _deactivatedCount - 1; i >= 0; i--)
        {
            int id = _deactivated[i];
            Body body = _registry[id];
            if (body == null) { RemoveAt(i); continue; }
            float dx = body.Position.X - focusPointSimUnits.X;
            float dy = body.Position.Y - focusPointSimUnits.Y;
            if (dx * dx + dy * dy <= _deactivationRadiusSq) { body.Enabled = true; RemoveAt(i); }
        }

        for (int id = 0; id < _registry.Length; id++)
        {
            Body body = _registry[id];
            if (body == null || !body.Enabled || body.BodyType != BodyType.Dynamic) continue;
            float dx = body.Position.X - focusPointSimUnits.X;
            float dy = body.Position.Y - focusPointSimUnits.Y;
            if (dx * dx + dy * dy > _deactivationRadiusSq) { body.Enabled = false; _deactivated[_deactivatedCount++] = id; }
        }
    }

    private void RemoveAt(int idx) { _deactivated[idx] = _deactivated[--_deactivatedCount]; }
}
