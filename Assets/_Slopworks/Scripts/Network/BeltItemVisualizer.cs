using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Client-side belt item visualization. Renders small cubes at item positions
/// synced from the server via NetworkBeltSegment.
/// </summary>
public class BeltItemVisualizer : MonoBehaviour
{
    private NetworkBeltSegment _netBelt;
    private readonly List<GameObject> _itemVisuals = new();
    private readonly List<Vector3> _positions = new();

    private static Material _sharedItemMat;

    public void Init(NetworkBeltSegment netBelt)
    {
        _netBelt = netBelt;
    }

    private void LateUpdate()
    {
        if (_netBelt == null) return;

        _netBelt.GetItemWorldPositions(_positions);

        // Grow pool if needed
        while (_itemVisuals.Count < _positions.Count)
        {
            var cube = GameObject.CreatePrimitive(PrimitiveType.Cube);
            cube.name = "BeltItemVisual";
            cube.transform.localScale = new Vector3(0.2f, 0.2f, 0.2f);
            cube.layer = PhysicsLayers.Decal;

            var col = cube.GetComponent<Collider>();
            if (col != null) Destroy(col);

            if (_sharedItemMat == null)
            {
                var renderer = cube.GetComponent<Renderer>();
                _sharedItemMat = new Material(renderer.sharedMaterial);
                _sharedItemMat.color = new Color(0.9f, 0.6f, 0.2f);
            }
            cube.GetComponent<Renderer>().sharedMaterial = _sharedItemMat;

            _itemVisuals.Add(cube);
        }

        // Position active visuals
        for (int i = 0; i < _positions.Count; i++)
        {
            _itemVisuals[i].SetActive(true);
            _itemVisuals[i].transform.position = _positions[i] + Vector3.up * 0.15f;
        }

        // Hide extras
        for (int i = _positions.Count; i < _itemVisuals.Count; i++)
            _itemVisuals[i].SetActive(false);
    }

    private void OnDestroy()
    {
        for (int i = 0; i < _itemVisuals.Count; i++)
        {
            if (_itemVisuals[i] != null)
                Destroy(_itemVisuals[i]);
        }
        _itemVisuals.Clear();
    }
}
