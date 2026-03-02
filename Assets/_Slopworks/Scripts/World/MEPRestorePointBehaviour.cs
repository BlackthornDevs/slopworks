using UnityEngine;

/// <summary>
/// MonoBehaviour wrapper for MEPRestorePoint. Implements IInteractable for player E key.
/// Visual: colored cube -- dark red when broken, green when restored.
/// Placed on PhysicsLayers.Interactable for InteractionController raycast detection.
/// </summary>
public class MEPRestorePointBehaviour : MonoBehaviour, IInteractable
{
    private MEPRestorePoint _point;
    private BuildingState _buildingState;
    private Renderer _renderer;

    private static readonly Color BrokenColor = new Color(0.5f, 0.1f, 0.1f);
    private static readonly Color RestoredColor = new Color(0.1f, 0.7f, 0.1f);

    public void Initialize(MEPRestorePoint point, BuildingState buildingState)
    {
        _point = point;
        _buildingState = buildingState;
        _renderer = GetComponent<Renderer>();
        UpdateVisual();
    }

    public string GetInteractionPrompt()
    {
        if (_point == null)
            return "";

        if (_point.IsRestored)
            return $"{_point.SystemType}: restored";

        return $"press E to restore {_point.SystemType}";
    }

    public void Interact(GameObject player)
    {
        if (_point == null || _buildingState == null)
            return;

        if (_point.IsRestored)
            return;

        _buildingState.RestorePoint(_point.PointId);
        UpdateVisual();
        Debug.Log($"mep: {_point.SystemType} restored at {_point.PointId}");
    }

    private void UpdateVisual()
    {
        if (_renderer == null)
            return;

        var color = _point != null && _point.IsRestored ? RestoredColor : BrokenColor;
        var mat = new Material(_renderer.sharedMaterial);
        mat.color = color;
        _renderer.sharedMaterial = mat;
    }
}
