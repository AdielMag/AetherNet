using nkast.Aether.Physics2D.Dynamics;

namespace AetherNet
{
    public interface IAetherColliderProvider
    {
        void AttachToBody(Body body, PhysicsWorldManager world);
    }
}
