public static class PhysicsLayers
{
    // Layer numbers (must match Edit > Project Settings > Tags and Layers)
    public const int Player = 8;
    public const int Fauna = 9;
    public const int Projectile = 10;
    public const int BIM_Static = 11;
    public const int Terrain = 12;
    public const int Structures = 13;
    public const int Interactable = 14;
    public const int GridPlane = 15;
    public const int VolumeTrigger = 16;
    public const int NavMeshAgent = 17;
    public const int Decal = 18;
    public const int FogOfWar = 19;

    // Raycast masks -- compute once at class load, reuse everywhere
    public static readonly int PlacementMask =
        (1 << Terrain) | (1 << BIM_Static) | (1 << GridPlane);

    public static readonly int WeaponHitMask =
        (1 << Player) | (1 << Fauna) | (1 << BIM_Static) | (1 << Structures);

    public static readonly int InteractMask =
        (1 << Interactable);

    public static readonly int FaunaMask =
        (1 << Fauna);

    public static readonly int FaunaLOSMask =
        (1 << BIM_Static) | (1 << Structures);
}
