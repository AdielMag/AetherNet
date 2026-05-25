<div align="center">
  <h1>⚡ AetherNet</h1>
  <p><strong>GC-free, deterministic 2D physics for server-authoritative Unity games</strong></p>

  <p>
    <img alt="Build" src="https://img.shields.io/github/actions/workflow/status/adielmag/aethernet/build.yml?style=flat-square&logo=github">
    <img alt="Coverage" src="https://img.shields.io/codecov/c/github/adielmag/aethernet?style=flat-square&logo=codecov">
    <a href="https://www.nuget.org/packages/AetherNet.Shared"><img alt="NuGet AetherNet.Shared" src="https://img.shields.io/badge/NuGet-AetherNet.Shared-blue?style=flat-square&logo=nuget"></a>
    <a href="https://www.nuget.org/packages/AetherNet.Unity"><img alt="NuGet AetherNet.Unity" src="https://img.shields.io/badge/NuGet-AetherNet.Unity-blue?style=flat-square&logo=nuget"></a>
    <img alt="License" src="https://img.shields.io/github/license/adielmag/aethernet?style=flat-square">
    <img alt=".NET" src="https://img.shields.io/badge/.NET-8.0-512BD4?style=flat-square&logo=dotnet">
  </p>
</div>

---

## What is AetherNet?

AetherNet completely decouples 2D physics simulation from Unity’s native engine. A single, deterministic physics loop powered by [Aether.Physics2D](https://github.com/nkast/Aether.Physics2D) (a pure C# Box2D port) runs identically on a headless .NET 8 server and a Unity client — with zero runtime heap allocation.

---

## Packages

| Package | Target | Description |
|---|---|---|
| `AetherNet.Shared` | .NET Standard 2.0 / .NET 8 | Core simulation: `PhysicsWorldManager`, collision, networking utilities, queries. Used on both server and client. |
| `AetherNet.Unity` | Unity (via NuGetForUnity) | MonoBehaviour components: `AetherRigidbody`, colliders, `AetherViewManager`, physics queries, scene baker, editor gizmos. |

---

## Install

### Server / .NET project

```
dotnet add package AetherNet.Shared
```

### Unity project

1. Install [NuGetForUnity](https://github.com/GlitchEnzo/NuGetForUnity) (free, open source).
2. In Unity: **NuGet → Manage NuGet Packages**, search for **AetherNet.Unity** and install.
   - `AetherNet.Shared` is pulled in automatically as a dependency.

---

## Features

- ⚡ **Zero runtime GC allocation** — pre-allocated parallel arrays throughout
- 🎯 **100% deterministic fixed-timestep simulation** — identical results on server and client
- 🖥️ **Headless .NET 8 server** — no Unity license required; loads baked JSON map files
- 💥 **Unity-style collision callbacks** — `OnCollisionEnter/Exit`, `OnTriggerEnter/Exit` via interface dispatch
- 💪 **Full force API** — `AddForce`, `AddTorque`, `AddForceAtPosition` with `ForceMode`
- 🔍 **Physics queries** — `Raycast`, `OverlapCircle`, `OverlapBox` with zero-alloc buffers
- 🏗️ **Editor scene baker** — **AetherNet → Bake Scene to JSON** exports map data for the headless server
- 🎨 **Scene View gizmos** — box, circle, and polygon colliders drawn in the editor
- 🔗 **Transport-agnostic networking** — plug in Mirror, FishNet, LiteNetLib, or raw sockets
- 🔒 **Rigidbody constraints** — `FreezePositionX/Y`, `FreezeRotation` applied post-step

---

## Quick Start — Unity

### Scene Setup

1. Create a GameObject → Add **AetherNet → View Manager**.
2. On entity prefabs, add **AetherNet → Rigidbody** + one of **Box / Circle / Polygon Collider**.
3. Bake the scene for the server: **AetherNet → Bake Scene to JSON**.

### Physics & Collisions

```csharp
using AetherNet;

public class Player : MonoBehaviour, IAetherCollisionHandler, IAetherTriggerHandler
{
    private AetherRigidbody _rb;
    void Awake() => _rb = GetComponent<AetherRigidbody>();

    void Update()
    {
        if (Input.GetKey(KeyCode.Space))
            _rb.AddForce(Vector2.up * 500f, ForceMode.Impulse);
    }

    public void OnCollisionEnter(ref CollisionData d) => Debug.Log($"Hit entity {d.EntityIdB}");
    public void OnCollisionExit(ref CollisionData d)  { }
    public void OnTriggerEnter(ref TriggerData d)     => Debug.Log($"Trigger {d.OtherEntityId}");
    public void OnTriggerExit(ref TriggerData d)      { }
}
```

---

## Quick Start — Headless Server

```bash
cd src/AetherNet.Server
dotnet run -- maps/level01.json
```

```csharp
var world  = new PhysicsWorldManager(WorldConfig.Default);
var loader = new MapLoader();
loader.LoadInto(world, "maps/level01.json");

var loop = new ServerTickLoop(world);
loop.SetSnapshotCallback((states, count, tick) =>
{
    int bytes = StateSerializer.Serialize(states, count, sendBuffer, 0);
    myTransport.BroadcastUnreliable(sendBuffer, bytes);
});
loop.Run(CancellationToken.None);
```

---

## Networking

| Type | Purpose |
|---|---|
| `INetworkStateProvider` | Hook into the tick loop — implement to broadcast state |
| `StateSerializer` | Zero-alloc binary write/read of `EntityState[]` |
| `StateInterpolator` | Client-side snapshot lerp for smooth rendering |
| `SnapshotBuffer` | Circular buffer of authoritative snapshots |
| `TickAcknowledger` | Bitmask ack tracking for delta compression |

See **[`examples/LiteNetLibExample/`](examples/LiteNetLibExample/)** for a complete LiteNetLib integration.

---

## Contributing

1. Fork and branch: `feature/your-feature` or `fix/issue`.
2. All `AetherNet.Shared` changes must pass `dotnet test`.
3. PR checklist: build clean, tests green, no new GC allocs in hot paths.

---

## License

[MIT](LICENSE)
