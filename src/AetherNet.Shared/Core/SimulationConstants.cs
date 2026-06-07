namespace AetherNet;

/// <summary>
/// 2D-to-3D coordinate plane mapping.
/// XY: Aether (x,y) → Unity (x, y, 0) — side-view / platformer.
/// XZ: Aether (x,y) → Unity (x, 0, y) — top-down.
/// </summary>
public enum SimulationPlane { XY, XZ }

public static class SimulationConstants
{
    public const float FixedTimestep       = 1f / 60f;
    public const int   VelocityIterations  = 8;
    public const int   PositionIterations  = 3;
    public const int   MaxBodies           = 5000;
    public const int   MaxFixtures         = 10000;
    public const int   MaxContacts         = 20000;

    /// <summary>
    /// Conversion factor from Unity world units to simulation (Box2D) meters.
    /// Default 100 (1 Unity unit = 100 pixels = 1 m).  Set to 1 for 1:1 mapping.
    /// </summary>
    public static float PixelsPerMeter     = 100f;

    /// <summary>
    /// Which Unity plane the 2D simulation maps onto.
    /// Default XY (side-view).  Set to XZ for top-down games.
    /// </summary>
    public static SimulationPlane Plane    = SimulationPlane.XY;
}
