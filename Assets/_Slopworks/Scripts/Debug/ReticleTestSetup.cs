using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

/// <summary>
/// Drop this on any GameObject to test the new reticle + build tooltip system.
/// Creates its own Canvas. Press B to toggle build mode, D/S/Z/C/V to switch modes.
/// Hides the old PlayerHUD crosshair and build mode text if present.
/// </summary>
public class ReticleTestSetup : MonoBehaviour
{
    private VisorHUD _visor;
    private ReticleController _reticle;
    private BuildTooltipUI _tooltip;
    private bool _buildMode;
    private int _activeKeycapIndex;
    private Canvas _canvas;

    private struct ModeEntry
    {
        public Key InputKey;
        public ReticleStyle Style;
        public string Label;
        public int KeycapIndex;

        public ModeEntry(Key key, ReticleStyle style, string label, int keycapIndex)
        {
            InputKey = key;
            Style = style;
            Label = label;
            KeycapIndex = keycapIndex;
        }
    }

    private static readonly ModeEntry[] BuildModes =
    {
        new(Key.F, ReticleStyle.BuildDefault,  "default",  -1),
        new(Key.T, ReticleStyle.BuildStraight,  "straight", -1),
        new(Key.Z, ReticleStyle.BuildZoop,      "zoop",      3),
        new(Key.C, ReticleStyle.BuildCurved,    "curved",   -1),
        new(Key.V, ReticleStyle.BuildVertical,  "vertical", -1),
    };

    private void Start()
    {
        // create overlay canvas
        var canvasObj = new GameObject("ReticleCanvas");
        _canvas = canvasObj.AddComponent<Canvas>();
        _canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        _canvas.sortingOrder = 100; // on top of everything
        canvasObj.AddComponent<CanvasScaler>().uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        canvasObj.GetComponent<CanvasScaler>().referenceResolution = new Vector2(1920, 1080);
        canvasObj.AddComponent<GraphicRaycaster>();

        // visor (persistent HUD elements — renders behind everything)
        var visorObj = new GameObject("Visor");
        visorObj.transform.SetParent(canvasObj.transform, false);
        _visor = visorObj.AddComponent<VisorHUD>();
        _visor.Build(canvasObj.transform);

        // reticle
        var reticleObj = new GameObject("Reticle");
        reticleObj.transform.SetParent(canvasObj.transform, false);
        _reticle = reticleObj.AddComponent<ReticleController>();

        // build tooltip (keycap row + action keys)
        var tooltipObj = new GameObject("BuildTooltip");
        tooltipObj.transform.SetParent(canvasObj.transform, false);
        _tooltip = tooltipObj.AddComponent<BuildTooltipUI>();
        _tooltip.Build(canvasObj.transform);

        Debug.Log("visor test: ready. B=build mode, F/T/Z/C/V=switch modes");
    }

    private void Update()
    {
        var kb = Keyboard.current;
        if (kb == null) return;

        // toggle build mode
        if (kb.bKey.wasPressedThisFrame)
        {
            _buildMode = !_buildMode;
            if (_buildMode)
            {
                _reticle.SetStyleWithLabel(ReticleStyle.BuildDefault, "build mode");
                _tooltip.SetVisible(true);
                _tooltip.SetActiveKeycap(-1);
                _activeKeycapIndex = -1;
                Debug.Log("visor test: build mode ON");
            }
            else
            {
                _reticle.SetStyleWithLabel(ReticleStyle.Gameplay, null);
                _tooltip.SetVisible(false);
                Debug.Log("visor test: build mode OFF");
            }
        }

        if (!_buildMode) return;

        // switch build sub-modes
        for (int i = 0; i < BuildModes.Length; i++)
        {
            if (kb[BuildModes[i].InputKey].wasPressedThisFrame)
            {
                _reticle.SetStyleWithLabel(BuildModes[i].Style, BuildModes[i].Label);
                _activeKeycapIndex = BuildModes[i].KeycapIndex;
                _tooltip.SetActiveKeycap(_activeKeycapIndex);
                Debug.Log("visor test: mode=" + BuildModes[i].Label);
            }
        }
    }
}
