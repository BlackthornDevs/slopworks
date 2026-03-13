using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// Playtest bootstrapper for belt system. Drop on empty GameObject, hit Play.
/// Tests: route building, mesh baking, item flow, visual positioning.
///
/// Controls:
/// - Left click: set belt start / confirm belt end
/// - Right click: cancel belt placement
/// - P: pre-seed a straight belt with flowing items
/// - I: insert item onto the first belt
/// - Space: tick simulation manually
/// </summary>
public class BeltPlaytestSetup : MonoBehaviour
{
    [SerializeField] private bool _preSeedBelt = true;
    [SerializeField] private Material _beltMaterial;

    private BeltNetwork _beltNetwork;
    private List<BeltSegment> _segments = new();
    private List<List<BeltRouteBuilder.Waypoint>> _routes = new();
    private List<float> _routeLengths = new();
    private List<GameObject> _beltObjects = new();

    private bool _pickingStart = true;
    private Vector3 _startPos;
    private Vector3 _startDir;
    private LineRenderer _previewLine;

    private List<GameObject> _itemVisuals = new();
    private List<float> _positionBuffer = new();

    private void Awake()
    {
        _beltNetwork = new BeltNetwork();

        if (_beltMaterial == null)
        {
            _beltMaterial = new Material(Shader.Find("Universal Render Pipeline/Lit"));
            _beltMaterial.color = new Color(0.3f, 0.3f, 0.35f);
        }

        var ground = GameObject.CreatePrimitive(PrimitiveType.Plane);
        ground.transform.localScale = new Vector3(10, 1, 10);
        ground.GetComponent<Renderer>().material.color = new Color(0.2f, 0.2f, 0.2f);

        var lineObj = new GameObject("BeltPreview");
        _previewLine = lineObj.AddComponent<LineRenderer>();
        _previewLine.startWidth = 0.2f;
        _previewLine.endWidth = 0.2f;
        _previewLine.positionCount = 30;
        _previewLine.material = new Material(Shader.Find("Sprites/Default"));
        _previewLine.startColor = Color.green;
        _previewLine.endColor = Color.green;
        lineObj.SetActive(false);

        if (_preSeedBelt)
            PreSeed();

        Debug.Log("belt playtest: ready. left-click to place belts, P to pre-seed, I to insert item, space to tick");
    }

    private void PreSeed()
    {
        var start = new Vector3(0, 0.5f, 0);
        var end = new Vector3(10, 0.5f, 0);
        PlaceBelt(start, Vector3.right, end, Vector3.right);

        if (_segments.Count > 0)
        {
            _segments[0].TryInsertAtStart("iron_ore", 50);
            _segments[0].TryInsertAtStart("copper_ore", 50);
            Debug.Log("belt playtest: pre-seeded straight belt with 2 items");
        }
    }

    private void PlaceBelt(Vector3 startPos, Vector3 startDir, Vector3 endPos, Vector3 endDir)
    {
        var validation = BeltPlacementValidator.Validate(startPos, startDir, endPos, endDir);
        if (!validation.IsValid)
        {
            Debug.Log($"belt playtest: placement rejected: {validation.Error}");
            return;
        }

        var waypoints = BeltRouteBuilder.Build(startPos, startDir, endPos, endDir, BeltRoutingMode.Default);
        float routeLength = BeltRouteBuilder.ComputeRouteLength(waypoints);
        var segment = BeltSegment.FromArcLength(routeLength);

        var midpoint = BeltRouteBuilder.EvaluateRoute(waypoints, routeLength, 0.5f);
        var go = new GameObject($"Belt_{_segments.Count}");
        go.transform.position = midpoint;

        BeltSplineMeshBaker.BakeMesh(go, waypoints, _beltMaterial);

        _segments.Add(segment);
        _routes.Add(waypoints);
        _routeLengths.Add(routeLength);
        _beltObjects.Add(go);

        if (_segments.Count > 1)
        {
            _beltNetwork.Connect(_segments[_segments.Count - 2], segment);
            Debug.Log($"belt playtest: wired belt {_segments.Count - 2} -> {_segments.Count - 1}");
        }

        Debug.Log($"belt playtest: placed belt {_segments.Count - 1}, route={routeLength:F1}m, subs={segment.TotalLength}");
    }

    private void Update()
    {
        var keyboard = Keyboard.current;
        var mouse = Mouse.current;
        if (keyboard == null || mouse == null) return;

        if (keyboard.spaceKey.wasPressedThisFrame)
        {
            for (int i = 0; i < _segments.Count; i++)
                _segments[i].Tick(10);
            _beltNetwork.Tick();
            Debug.Log("belt playtest: ticked simulation");
        }

        if (keyboard.iKey.wasPressedThisFrame && _segments.Count > 0)
        {
            bool inserted = _segments[0].TryInsertAtStart("iron_ore", 50);
            Debug.Log($"belt playtest: insert item: {(inserted ? "success" : "failed")}");
        }

        if (keyboard.pKey.wasPressedThisFrame)
            PreSeed();

        HandleMousePlacement(mouse);
        UpdateItemVisuals();
    }

    private void HandleMousePlacement(Mouse mouse)
    {
        var cam = Camera.main;
        if (cam == null) return;

        var ray = cam.ScreenPointToRay(mouse.position.ReadValue());
        if (!Physics.Raycast(ray, out var hit, 200f)) return;

        if (_pickingStart)
        {
            if (mouse.leftButton.wasPressedThisFrame)
            {
                _startPos = hit.point;
                var camFwd = cam.transform.forward;
                camFwd.y = 0;
                _startDir = camFwd.normalized;
                _pickingStart = false;
                _previewLine.gameObject.SetActive(true);
                Debug.Log($"belt playtest: start set at {hit.point}");
            }
        }
        else
        {
            var endDir = (hit.point - _startPos);
            endDir.y = 0;
            endDir = endDir.sqrMagnitude > 0.001f ? endDir.normalized : _startDir;

            var waypoints = BeltRouteBuilder.Build(_startPos, _startDir, hit.point, endDir, BeltRoutingMode.Default);
            float routeLen = BeltRouteBuilder.ComputeRouteLength(waypoints);
            var validation = BeltPlacementValidator.Validate(_startPos, _startDir, hit.point, endDir);

            var color = validation.IsValid ? Color.green : Color.red;
            _previewLine.startColor = color;
            _previewLine.endColor = color;

            for (int i = 0; i < 30; i++)
            {
                float t = (float)i / 29;
                _previewLine.SetPosition(i, BeltRouteBuilder.EvaluateRoute(waypoints, routeLen, t));
            }

            if (mouse.leftButton.wasPressedThisFrame && validation.IsValid)
            {
                PlaceBelt(_startPos, _startDir, hit.point, endDir);
                _pickingStart = true;
                _previewLine.gameObject.SetActive(false);
            }

            if (mouse.rightButton.wasPressedThisFrame)
            {
                _pickingStart = true;
                _previewLine.gameObject.SetActive(false);
                Debug.Log("belt playtest: placement cancelled");
            }
        }
    }

    private void UpdateItemVisuals()
    {
        foreach (var v in _itemVisuals)
            if (v != null) Destroy(v);
        _itemVisuals.Clear();

        for (int s = 0; s < _segments.Count; s++)
        {
            _positionBuffer.Clear();
            _segments[s].GetItemPositions(_positionBuffer);
            var items = _segments[s].GetItems();

            for (int i = 0; i < _positionBuffer.Count; i++)
            {
                float t = _positionBuffer[i];
                var worldPos = BeltRouteBuilder.EvaluateRoute(_routes[s], _routeLengths[s], t);

                var sphere = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                sphere.transform.position = worldPos;
                sphere.transform.localScale = Vector3.one * 0.15f;
                DestroyImmediate(sphere.GetComponent<Collider>());

                var itemColor = items[i].itemId == "iron_ore"
                    ? new Color(0.6f, 0.3f, 0.2f)
                    : new Color(0.8f, 0.5f, 0.2f);
                sphere.GetComponent<Renderer>().material.color = itemColor;

                _itemVisuals.Add(sphere);
            }
        }
    }

    private void OnGUI()
    {
        int y = 10;
        GUI.Label(new Rect(10, y, 400, 25), $"belts: {_segments.Count}");
        y += 20;

        for (int i = 0; i < _segments.Count; i++)
        {
            var seg = _segments[i];
            GUI.Label(new Rect(10, y, 500, 25),
                $"  belt {i}: items={seg.ItemCount} gap={seg.TerminalGap} len={seg.TotalLength}");
            y += 18;
        }

        y += 10;
        GUI.Label(new Rect(10, y, 400, 25), "controls: click=place, space=tick, I=insert, P=preseed");
    }
}
