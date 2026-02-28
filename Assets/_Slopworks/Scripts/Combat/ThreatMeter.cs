using UnityEngine;

public class ThreatMeter
{
    public float ThreatLevel { get; private set; }

    public void AddThreat(float amount)
    {
        ThreatLevel = Mathf.Clamp01(ThreatLevel + amount);
    }

    public int ScaleEnemyCount(int baseCount)
    {
        // at 0 threat: 1x enemies, at 1.0 threat: 2x enemies
        float multiplier = 1f + ThreatLevel;
        return Mathf.CeilToInt(baseCount * multiplier);
    }
}
