using UnityEngine;

/// <summary>
/// Defines the visual appearance of the reticle for a given mode.
/// Adding a new mode = adding a new static field here. Nothing else changes.
/// </summary>
public struct ReticleStyle
{
    public string Left;
    public string Center;
    public string Right;
    public Color Color;

    public ReticleStyle(string left, string center, string right, Color color)
    {
        Left = left;
        Center = center;
        Right = right;
        Color = color;
    }

    // gameplay
    public static readonly ReticleStyle Gameplay = new("[", "+", "]", new Color(0f, 0.86f, 1f, 0.7f));

    // build modes
    public static readonly ReticleStyle BuildDefault  = new("[", "+", "]", new Color(1f, 0.67f, 0.19f, 0.7f));
    public static readonly ReticleStyle BuildStraight = new("|", "+", "|", new Color(1f, 0.67f, 0.19f, 0.7f));
    public static readonly ReticleStyle BuildZoop     = new("[", "Z", "]", new Color(1f, 0.67f, 0.19f, 0.8f));
    public static readonly ReticleStyle BuildCurved   = new("(", "*", ")", new Color(1f, 0.67f, 0.19f, 0.7f));
    public static readonly ReticleStyle BuildVertical = new("[", "^", "]", new Color(1f, 0.67f, 0.19f, 0.7f));
}
