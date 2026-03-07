using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

/// <summary>
/// Draws a grid overlay on the terrain around the player.
/// Toggle with G key while in build mode.
/// Uses a runtime mesh with Lines topology for URP compatibility.
/// </summary>
public class GridOverlay : MonoBehaviour
{
    public bool Visible;
    public int Radius = 20; // cells around player
    public Color GridColor = new(0.3f, 0.8f, 1f, 0.4f);
    public Color FoundationGridColor = new(1f, 0.8f, 0.2f, 0.5f);

    private Mesh _mesh;
    private Material _material;
    private Camera _camera;
    private Vector2Int _lastCenter;

    private static readonly int TerrainMask = 1 << PhysicsLayers.Terrain;

    public void Init(Camera cam)
    {
        _camera = cam;
        _mesh = new Mesh { name = "GridOverlay" };
        _mesh.MarkDynamic();

        var shader = Shader.Find("Sprites/Default");
        if (shader == null) shader = Shader.Find("Universal Render Pipeline/Unlit");
        _material = new Material(shader);
        _material.hideFlags = HideFlags.HideAndDontSave;
        _material.SetFloat("_ZWrite", 0);
        _material.renderQueue = (int)RenderQueue.Transparent;
    }

    private void LateUpdate()
    {
        if (!Visible || _camera == null || _material == null) return;

        float cs = FactoryGrid.CellSize;
        Vector3 playerPos = transform.position;
        int centerX = Mathf.FloorToInt(playerPos.x / cs);
        int centerZ = Mathf.FloorToInt(playerPos.z / cs);
        var center = new Vector2Int(centerX, centerZ);

        if (center != _lastCenter)
        {
            _lastCenter = center;
            RebuildMesh(centerX, centerZ);
        }

        Graphics.DrawMesh(_mesh, Matrix4x4.identity, _material, 0, _camera);
    }

    private void RebuildMesh(int centerX, int centerZ)
    {
        float cs = FactoryGrid.CellSize;
        int fs = FactoryGrid.FoundationSize;
        float yOffset = 0.05f;

        int gridSize = Radius * 2 + 1;
        // Each cell contributes 2 lines = 4 vertices
        int vertCount = gridSize * gridSize * 4;

        var verts = new List<Vector3>(vertCount);
        var colors = new List<Color>(vertCount);
        var indices = new List<int>(vertCount);

        for (int x = centerX - Radius; x <= centerX + Radius; x++)
        {
            for (int z = centerZ - Radius; z <= centerZ + Radius; z++)
            {
                bool isFoundationLine = (x % fs == 0) || (z % fs == 0);
                Color c = isFoundationLine ? FoundationGridColor : GridColor;

                float wx = x * cs;
                float wz = z * cs;

                float y1 = GetTerrainY(wx, wz) + yOffset;
                float y2 = GetTerrainY(wx + cs, wz) + yOffset;
                float y3 = GetTerrainY(wx, wz + cs) + yOffset;

                int idx = verts.Count;

                // Horizontal line (along X)
                verts.Add(new Vector3(wx, y1, wz));
                verts.Add(new Vector3(wx + cs, y2, wz));
                colors.Add(c);
                colors.Add(c);
                indices.Add(idx);
                indices.Add(idx + 1);

                // Vertical line (along Z)
                verts.Add(new Vector3(wx, y1, wz));
                verts.Add(new Vector3(wx, y3, wz + cs));
                colors.Add(c);
                colors.Add(c);
                indices.Add(idx + 2);
                indices.Add(idx + 3);
            }
        }

        _mesh.Clear();
        _mesh.SetVertices(verts);
        _mesh.SetColors(colors);
        _mesh.SetIndices(indices, MeshTopology.Lines, 0);
    }

    private float GetTerrainY(float x, float z)
    {
        var ray = new Ray(new Vector3(x, 500f, z), Vector3.down);
        if (Physics.Raycast(ray, out var hit, 1000f, TerrainMask))
            return hit.point.y;
        return 0f;
    }

    private void OnDestroy()
    {
        if (_mesh != null) Destroy(_mesh);
        if (_material != null) Destroy(_material);
    }
}
