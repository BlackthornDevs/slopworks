using UnityEngine;

/// <summary>
/// A page of hotbar entries. Page 0 is typically inventory items,
/// page 1 is build tools, etc.
/// </summary>
public class HotbarPage
{
    public string PageName;
    public HotbarEntry[] Entries;

    public HotbarPage(string pageName, int slotCount)
    {
        PageName = pageName;
        Entries = new HotbarEntry[slotCount];
        for (int i = 0; i < slotCount; i++)
            Entries[i] = new HotbarEntry();
    }
}

/// <summary>
/// A single entry on a hotbar page. For build tools, Id maps to a tool mode.
/// </summary>
public class HotbarEntry
{
    public string Id;
    public string DisplayName;
    public Color Color = new Color(0.5f, 0.5f, 0.5f, 0.5f);
}
