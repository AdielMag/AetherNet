namespace AetherNet;

public static class PhysicsLayers
{
    public const int Default     = 0;
    public const int Player      = 1;
    public const int Environment = 2;
    public const int Projectile  = 3;
    public const int Trigger     = 4;

    public static int MaskFor(int layer) => layer switch
    {
        Default     => All,
        Player      => Bit(Default) | Bit(Environment) | Bit(Trigger),
        Environment => All,
        Projectile  => Bit(Default) | Bit(Environment) | Bit(Player),
        Trigger     => Bit(Player),
        _           => All,
    };

    public static int Bit(int n) => 1 << n;
    public const int All  = 0xFFFF;
    public const int None = 0;
}
