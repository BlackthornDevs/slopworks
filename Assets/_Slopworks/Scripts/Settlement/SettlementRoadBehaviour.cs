using UnityEngine;

public class SettlementRoadBehaviour : MonoBehaviour
{
    private LineRenderer _lineRenderer;
    private string _buildingIdA;
    private string _buildingIdB;

    public string BuildingIdA => _buildingIdA;
    public string BuildingIdB => _buildingIdB;

    public void Initialize(string idA, string idB, Vector3 posA, Vector3 posB)
    {
        _buildingIdA = idA;
        _buildingIdB = idB;

        _lineRenderer = gameObject.AddComponent<LineRenderer>();
        _lineRenderer.positionCount = 2;
        _lineRenderer.SetPosition(0, posA + Vector3.up * 0.5f);
        _lineRenderer.SetPosition(1, posB + Vector3.up * 0.5f);
        _lineRenderer.startWidth = 2f;
        _lineRenderer.endWidth = 2f;
        _lineRenderer.material = new Material(Shader.Find("Universal Render Pipeline/Lit"));
        _lineRenderer.material.color = new Color(0.6f, 0.5f, 0.35f, 1f);
        _lineRenderer.useWorldSpace = true;

        Debug.Log($"settlement: road visual created {idA} <-> {idB}");
    }
}
