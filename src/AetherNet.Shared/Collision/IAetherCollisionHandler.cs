namespace AetherNet.Collision;

public interface IAetherCollisionHandler
{
    void OnCollisionEnter(ref CollisionData collision);
    void OnCollisionExit (ref CollisionData collision);
}

public interface IAetherTriggerHandler
{
    void OnTriggerEnter(ref TriggerData trigger);
    void OnTriggerExit (ref TriggerData trigger);
}
