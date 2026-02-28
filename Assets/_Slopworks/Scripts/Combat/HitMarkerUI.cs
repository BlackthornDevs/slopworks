using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Hit marker that displays a brief crosshair-style indicator at screen center.
/// Add to the same GameObject as PlayerHUD (HUD_Canvas).
/// Call Show() from WeaponBehaviour when a hit is confirmed.
/// </summary>
public class HitMarkerUI : MonoBehaviour
{
    // Duration of the full fade
    private const float FadeDuration = 0.15f;
    // Half-length of each arm in pixels
    private const float ArmLength = 8f;
    // Thickness of each arm in pixels
    private const float ArmThickness = 2f;
    // Gap between center and arm start in pixels
    private const float CenterGap = 3f;

    private static readonly Color ColorStart = Color.white;
    private static readonly Color ColorEnd   = new Color(0.9f, 0.2f, 0.2f, 0f);

    // Four arms: top, bottom, left, right
    private Image[] _arms;
    private float   _timer;
    private bool    _active;

    private void Awake()
    {
        BuildUI();
        SetVisible(false);
    }

    private void Update()
    {
        if (!_active)
            return;

        _timer -= Time.deltaTime;
        float t = Mathf.Clamp01(1f - (_timer / FadeDuration)); // 0 at start, 1 at end
        Color c = Color.Lerp(ColorStart, ColorEnd, t);

        foreach (var arm in _arms)
            arm.color = c;

        if (_timer <= 0f)
        {
            _active = false;
            SetVisible(false);
        }
    }

    /// <summary>
    /// Triggers the hit marker animation. Safe to call every shot.
    /// </summary>
    public void Show()
    {
        _timer  = FadeDuration;
        _active = true;

        foreach (var arm in _arms)
            arm.color = ColorStart;

        SetVisible(true);
    }

    private void SetVisible(bool visible)
    {
        foreach (var arm in _arms)
            arm.gameObject.SetActive(visible);
    }

    private void BuildUI()
    {
        var canvas = GetComponent<Canvas>();
        if (canvas == null)
        {
            Debug.LogError("HitMarkerUI: no Canvas found on this GameObject");
            enabled = false;
            return;
        }

        // Container anchored to screen center, zero size
        var container = new GameObject("HitMarker");
        container.transform.SetParent(transform, false);
        var containerRect = container.AddComponent<RectTransform>();
        containerRect.anchorMin = new Vector2(0.5f, 0.5f);
        containerRect.anchorMax = new Vector2(0.5f, 0.5f);
        containerRect.pivot     = new Vector2(0.5f, 0.5f);
        containerRect.sizeDelta = Vector2.zero;
        containerRect.anchoredPosition = Vector2.zero;

        _arms = new Image[4];

        // Top arm
        _arms[0] = MakeArm(container.transform, "HitMarker_Top",
            new Vector2(0f, CenterGap + ArmLength * 0.5f),
            new Vector2(ArmThickness, ArmLength));

        // Bottom arm
        _arms[1] = MakeArm(container.transform, "HitMarker_Bottom",
            new Vector2(0f, -(CenterGap + ArmLength * 0.5f)),
            new Vector2(ArmThickness, ArmLength));

        // Left arm
        _arms[2] = MakeArm(container.transform, "HitMarker_Left",
            new Vector2(-(CenterGap + ArmLength * 0.5f), 0f),
            new Vector2(ArmLength, ArmThickness));

        // Right arm
        _arms[3] = MakeArm(container.transform, "HitMarker_Right",
            new Vector2(CenterGap + ArmLength * 0.5f, 0f),
            new Vector2(ArmLength, ArmThickness));
    }

    private static Image MakeArm(Transform parent, string name, Vector2 offset, Vector2 size)
    {
        var obj  = new GameObject(name);
        obj.transform.SetParent(parent, false);

        var rect         = obj.AddComponent<RectTransform>();
        rect.anchorMin   = new Vector2(0.5f, 0.5f);
        rect.anchorMax   = new Vector2(0.5f, 0.5f);
        rect.pivot       = new Vector2(0.5f, 0.5f);
        rect.sizeDelta   = size;
        rect.anchoredPosition = offset;

        var img           = obj.AddComponent<Image>();
        img.color         = Color.white;
        img.raycastTarget = false;

        return img;
    }
}
