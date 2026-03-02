using System.Collections.Generic;

/// <summary>
/// Pure C# manager for all supply lines (D-004).
/// Registers lines, ticks them each frame, provides queries.
/// </summary>
public class SupplyLineManager
{
    private readonly List<SupplyLine> _lines = new();

    public int LineCount => _lines.Count;

    public int TotalInFlight
    {
        get
        {
            int count = 0;
            for (int i = 0; i < _lines.Count; i++)
                count += _lines[i].InFlightCount;
            return count;
        }
    }

    public int TotalDelivered
    {
        get
        {
            int count = 0;
            for (int i = 0; i < _lines.Count; i++)
                count += _lines[i].TotalDelivered;
            return count;
        }
    }

    public void RegisterLine(SupplyLine line)
    {
        _lines.Add(line);
    }

    public void UnregisterLine(SupplyLine line)
    {
        _lines.Remove(line);
    }

    public IReadOnlyList<SupplyLine> GetLinesForSource(string buildingId)
    {
        var result = new List<SupplyLine>();
        for (int i = 0; i < _lines.Count; i++)
        {
            if (_lines[i].Source.BuildingId == buildingId)
                result.Add(_lines[i]);
        }
        return result;
    }

    public void TickAll(float deltaTime)
    {
        for (int i = 0; i < _lines.Count; i++)
            _lines[i].Tick(deltaTime);
    }
}
