namespace AetherNet;

public static class SimulationConstants
{
    public const float FixedTimestep       = 1f / 60f;
    public const int   VelocityIterations  = 8;
    public const int   PositionIterations  = 3;
    public const int   MaxBodies           = 5000;
    public const int   MaxFixtures         = 10000;
    public const int   MaxContacts         = 20000;
    public const float PixelsPerMeter      = 100f;
}
