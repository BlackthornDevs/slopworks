/// <summary>
/// Data class tracking a single MEP restore point inside a building.
/// Part of the building simulation layer (D-004).
/// </summary>
public class MEPRestorePoint
{
    public string PointId { get; }
    public MEPSystemType SystemType { get; }
    public bool IsRestored { get; private set; }

    public MEPRestorePoint(string pointId, MEPSystemType systemType)
    {
        PointId = pointId;
        SystemType = systemType;
    }

    /// <summary>
    /// Marks this point as restored. Returns true if it was newly restored,
    /// false if already restored (idempotent).
    /// </summary>
    public bool Restore()
    {
        if (IsRestored)
            return false;

        IsRestored = true;
        return true;
    }
}
