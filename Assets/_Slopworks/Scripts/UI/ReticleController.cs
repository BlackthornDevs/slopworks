using TMPro;
using UnityEngine;

/// <summary>
/// Manages the reticle display: three text elements (left bracket, center symbol,
/// right bracket) plus an optional mode label that flashes briefly on mode switch.
/// Built at runtime on a Canvas — no prefab dependency.
/// </summary>
public class ReticleController : MonoBehaviour
{
    private TextMeshProUGUI _leftText;
    private TextMeshProUGUI _centerText;
    private TextMeshProUGUI _rightText;
    private TextMeshProUGUI _modeLabel;
    private CanvasGroup _modeLabelGroup;

    private float _labelFadeTimer;
    private const float LabelShowDuration = 1.5f;
    private const float LabelFadeDuration = 0.4f;
    private const float ReticleSpacing = 2f;
    private const float ReticleSize = 36f;
    private const float CenterSize = 28f;
    private const float LabelSize = 11f;

    private ReticleStyle _currentStyle;

    public ReticleStyle CurrentStyle => _currentStyle;

    private void Awake()
    {
        BuildUI();
        SetStyle(ReticleStyle.Gameplay);
    }

    /// <summary>
    /// Set the reticle appearance. Call this whenever the mode changes.
    /// The mode label flashes the mode name briefly, then fades.
    /// </summary>
    public void SetStyle(ReticleStyle style)
    {
        _currentStyle = style;

        _leftText.text = style.Left;
        _centerText.text = style.Center;
        _rightText.text = style.Right;

        _leftText.color = style.Color;
        _centerText.color = style.Color;
        _rightText.color = style.Color;
    }

    /// <summary>
    /// Flash a mode label under the reticle. Pass null or empty to skip.
    /// </summary>
    public void ShowModeLabel(string text)
    {
        if (string.IsNullOrEmpty(text))
            return;

        _modeLabel.text = text.ToUpper();
        _modeLabel.color = _currentStyle.Color;
        _modeLabelGroup.alpha = 0.6f;
        _labelFadeTimer = LabelShowDuration;
    }

    /// <summary>
    /// Convenience: set style and flash the label in one call.
    /// </summary>
    public void SetStyleWithLabel(ReticleStyle style, string label)
    {
        SetStyle(style);
        ShowModeLabel(label);
    }

    private void Update()
    {
        if (_labelFadeTimer > 0f)
        {
            _labelFadeTimer -= Time.deltaTime;
            if (_labelFadeTimer <= LabelFadeDuration)
            {
                float t = Mathf.Clamp01(_labelFadeTimer / LabelFadeDuration);
                _modeLabelGroup.alpha = 0.6f * t;
            }
        }
    }

    private void BuildUI()
    {
        // container centered on screen
        var container = new GameObject("ReticleContainer");
        container.transform.SetParent(transform, false);
        var containerRect = container.AddComponent<RectTransform>();
        containerRect.anchorMin = new Vector2(0.5f, 0.5f);
        containerRect.anchorMax = new Vector2(0.5f, 0.5f);
        containerRect.sizeDelta = new Vector2(200f, 60f);
        containerRect.anchoredPosition = Vector2.zero;

        // horizontal layout for [ + ]
        var row = new GameObject("ReticleRow");
        row.transform.SetParent(container.transform, false);
        var rowRect = row.AddComponent<RectTransform>();
        rowRect.anchorMin = new Vector2(0.5f, 0.5f);
        rowRect.anchorMax = new Vector2(0.5f, 0.5f);
        rowRect.sizeDelta = new Vector2(200f, 40f);
        rowRect.anchoredPosition = Vector2.zero;

        var layout = row.AddComponent<UnityEngine.UI.HorizontalLayoutGroup>();
        layout.childAlignment = TextAnchor.MiddleCenter;
        layout.spacing = ReticleSpacing;
        layout.childControlWidth = false;
        layout.childControlHeight = false;
        layout.childForceExpandWidth = false;
        layout.childForceExpandHeight = false;

        _leftText = CreateTextElement(row.transform, "Left", ReticleSize, 30f);
        _centerText = CreateTextElement(row.transform, "Center", CenterSize, 30f);
        _rightText = CreateTextElement(row.transform, "Right", ReticleSize, 30f);

        // mode label below reticle
        var labelObj = new GameObject("ModeLabel");
        labelObj.transform.SetParent(container.transform, false);
        _modeLabel = labelObj.AddComponent<TextMeshProUGUI>();
        _modeLabel.fontSize = LabelSize;
        _modeLabel.alignment = TextAlignmentOptions.Center;
        _modeLabel.raycastTarget = false;
        _modeLabel.characterSpacing = 4f;

        var labelRect = _modeLabel.rectTransform;
        labelRect.anchorMin = new Vector2(0.5f, 0.5f);
        labelRect.anchorMax = new Vector2(0.5f, 0.5f);
        labelRect.sizeDelta = new Vector2(150f, 20f);
        labelRect.anchoredPosition = new Vector2(0f, -28f);

        _modeLabelGroup = labelObj.AddComponent<CanvasGroup>();
        _modeLabelGroup.alpha = 0f;
        _modeLabelGroup.blocksRaycasts = false;
        _modeLabelGroup.interactable = false;
    }

    private TextMeshProUGUI CreateTextElement(Transform parent, string name, float fontSize, float width)
    {
        var obj = new GameObject(name);
        obj.transform.SetParent(parent, false);
        var tmp = obj.AddComponent<TextMeshProUGUI>();
        tmp.fontSize = fontSize;
        tmp.alignment = TextAlignmentOptions.Center;
        tmp.raycastTarget = false;
        tmp.fontStyle = FontStyles.Normal;

        var rect = tmp.rectTransform;
        rect.sizeDelta = new Vector2(width, 40f);

        var fitter = obj.AddComponent<UnityEngine.UI.ContentSizeFitter>();
        fitter.horizontalFit = UnityEngine.UI.ContentSizeFitter.FitMode.PreferredSize;
        fitter.verticalFit = UnityEngine.UI.ContentSizeFitter.FitMode.PreferredSize;

        return tmp;
    }
}
