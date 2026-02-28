using System;
using UnityEngine;

/// <summary>
/// Defines a single I/O port on a machine, including its position
/// relative to the machine origin and the direction it faces.
/// </summary>
[Serializable]
public struct MachinePort
{
    /// <summary>
    /// Port position relative to the machine origin cell.
    /// </summary>
    public Vector2Int localOffset;

    /// <summary>
    /// Direction the port faces (e.g. (0,1) = north, (1,0) = east).
    /// </summary>
    public Vector2Int direction;

    /// <summary>
    /// Whether this port accepts input or emits output.
    /// </summary>
    public PortType type;
}
