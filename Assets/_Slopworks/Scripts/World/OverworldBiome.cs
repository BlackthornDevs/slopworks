using UnityEngine;

public enum OverworldBiomeType
{
    Grassland,
    Forest,
    Wasteland,
    Swamp,
    Ruins,
    OvergrownRuins
}

/// <summary>
/// Biome lookup from temperature/moisture and biome-to-color mapping.
/// </summary>
public static class OverworldBiomeLookup
{
    public static OverworldBiomeType GetBiome(float temperature, float moisture)
    {
        bool warm = temperature > 0.45f;
        if (moisture < 0.33f)
            return warm ? OverworldBiomeType.Wasteland : OverworldBiomeType.Ruins;
        if (moisture > 0.66f)
            return warm ? OverworldBiomeType.Swamp : OverworldBiomeType.OvergrownRuins;
        return warm ? OverworldBiomeType.Grassland : OverworldBiomeType.Forest;
    }

    public static Color GetColor(OverworldBiomeType biome)
    {
        switch (biome)
        {
            case OverworldBiomeType.Grassland:      return new Color(0.35f, 0.45f, 0.25f);
            case OverworldBiomeType.Forest:          return new Color(0.20f, 0.35f, 0.15f);
            case OverworldBiomeType.Wasteland:       return new Color(0.50f, 0.40f, 0.28f);
            case OverworldBiomeType.Swamp:           return new Color(0.20f, 0.30f, 0.25f);
            case OverworldBiomeType.Ruins:           return new Color(0.40f, 0.38f, 0.35f);
            case OverworldBiomeType.OvergrownRuins:  return new Color(0.30f, 0.38f, 0.28f);
            default:                                 return Color.magenta;
        }
    }
}
