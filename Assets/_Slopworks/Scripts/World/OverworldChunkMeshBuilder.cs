using UnityEngine;

/// <summary>
/// Builds a combined hex mesh for one chunk of the overworld grid.
/// Each hex has a center vertex + 6 corner vertices, 6 triangles.
/// Vertex colors set from biome type.
/// </summary>
public static class OverworldChunkMeshBuilder
{
    /// <summary>
    /// Build mesh for a chunk.
    /// heights[localQ, localR] = elevation. biomes[localQ, localR] = biome type.
    /// chunkQ, chunkR = chunk offset in hex coords (multiply by chunk size to get global offset).
    /// </summary>
    public static Mesh Build(float[,] heights, OverworldBiomeType[,] biomes, float hexSize,
        int chunkQ, int chunkR)
    {
        int width = heights.GetLength(0);
        int height = heights.GetLength(1);
        int hexCount = width * height;

        var vertices = new Vector3[hexCount * 7];
        var triangles = new int[hexCount * 18];
        var colors = new Color[hexCount * 7];

        int vi = 0;
        int ti = 0;

        for (int r = 0; r < height; r++)
        {
            for (int q = 0; q < width; q++)
            {
                int globalQ = chunkQ * width + q;
                int globalR = chunkR * height + r;

                var worldPos = HexGridUtility.HexToWorld(globalQ, globalR, hexSize);
                worldPos.y = heights[q, r];

                var corners = HexGridUtility.HexCorners(worldPos, hexSize);
                var biomeColor = OverworldBiomeLookup.GetColor(biomes[q, r]);

                int centerIdx = vi;
                vertices[vi] = worldPos;
                colors[vi] = biomeColor;
                vi++;

                for (int c = 0; c < 6; c++)
                {
                    vertices[vi] = corners[c];
                    colors[vi] = biomeColor;
                    vi++;
                }

                // 6 triangles: center to each edge (CCW winding for upward normals)
                for (int c = 0; c < 6; c++)
                {
                    triangles[ti++] = centerIdx;
                    triangles[ti++] = centerIdx + 1 + (c + 1) % 6;
                    triangles[ti++] = centerIdx + 1 + c;
                }
            }
        }

        var mesh = new Mesh();
        mesh.vertices = vertices;
        mesh.triangles = triangles;
        mesh.colors = colors;
        mesh.RecalculateNormals();
        mesh.RecalculateBounds();
        return mesh;
    }
}
