using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Build mode tooltip: a row of keycap boxes above the hotbar (center)
/// and a vertical stack of action keys on the right margin.
/// Visible only when build mode is active.
/// </summary>
public class BuildTooltipUI : MonoBehaviour
{
    private GameObject _keycapRow;
    private GameObject _actionStack;
    private RectTransform _rootRect;

    public void SetVisible(bool visible)
    {
        if (_keycapRow != null) _keycapRow.SetActive(visible);
        if (_actionStack != null) _actionStack.SetActive(visible);
    }

    /// <summary>
    /// Build all UI elements on the given Canvas.
    /// Call once after the Canvas exists.
    /// </summary>
    public void Build(Transform canvasTransform)
    {
        BuildKeycapRow(canvasTransform);
        BuildActionStack(canvasTransform);
        SetVisible(false);
    }

    /// <summary>
    /// Highlight the active keycap by index. -1 = none highlighted.
    /// </summary>
    public void SetActiveKeycap(int index)
    {
        if (_keycapRow == null) return;

        for (int i = 0; i < _keycapRow.transform.childCount; i++)
        {
            var cap = _keycapRow.transform.GetChild(i);
            var keyBg = cap.GetChild(0).GetComponent<Image>();
            var label = cap.GetChild(1).GetComponent<TextMeshProUGUI>();

            bool active = i == index;
            keyBg.color = active
                ? new Color(1f, 0.67f, 0.19f, 0.5f)
                : new Color(1f, 0.67f, 0.19f, 0.12f);
            label.color = active
                ? new Color(1f, 0.67f, 0.19f, 1f)
                : new Color(0.4f, 0.4f, 0.4f, 1f);
        }
    }

    private void BuildKeycapRow(Transform canvasTransform)
    {
        _keycapRow = new GameObject("BuildKeycapRow");
        _keycapRow.transform.SetParent(canvasTransform, false);
        var rect = _keycapRow.AddComponent<RectTransform>();
        rect.anchorMin = new Vector2(0.5f, 0f);
        rect.anchorMax = new Vector2(0.5f, 0f);
        rect.pivot = new Vector2(0.5f, 0f);
        rect.anchoredPosition = new Vector2(0f, 52f);
        rect.sizeDelta = new Vector2(400f, 40f);

        var layout = _keycapRow.AddComponent<HorizontalLayoutGroup>();
        layout.childAlignment = TextAnchor.MiddleCenter;
        layout.spacing = 4f;
        layout.childControlWidth = false;
        layout.childControlHeight = false;
        layout.childForceExpandWidth = false;
        layout.childForceExpandHeight = false;

        CreateKeycap(_keycapRow.transform, "R", "Rotate", 28f);
        CreateKeycap(_keycapRow.transform, "X", "Delete", 28f);
        CreateKeycap(_keycapRow.transform, "G", "Grid", 28f);
        CreateKeycap(_keycapRow.transform, "Z", "Zoop", 28f);
        CreateKeycap(_keycapRow.transform, "Tab", "Variant", 38f);
    }

    private void BuildActionStack(Transform canvasTransform)
    {
        _actionStack = new GameObject("BuildActionStack");
        _actionStack.transform.SetParent(canvasTransform, false);
        var rect = _actionStack.AddComponent<RectTransform>();
        rect.anchorMin = new Vector2(1f, 0f);
        rect.anchorMax = new Vector2(1f, 0f);
        rect.pivot = new Vector2(1f, 0f);
        rect.anchoredPosition = new Vector2(-14f, 70f);
        rect.sizeDelta = new Vector2(80f, 120f);

        var layout = _actionStack.AddComponent<VerticalLayoutGroup>();
        layout.childAlignment = TextAnchor.UpperRight;
        layout.spacing = 5f;
        layout.childControlWidth = false;
        layout.childControlHeight = false;
        layout.childForceExpandWidth = false;
        layout.childForceExpandHeight = false;

        CreateActionKey(_actionStack.transform, "LMB", "Place",
            new Color(0.55f, 0.8f, 0.55f, 1f), new Color(0.39f, 0.71f, 0.39f, 0.12f));
        CreateActionKey(_actionStack.transform, "RMB", "Remove",
            new Color(0.8f, 0.55f, 0.55f, 1f), new Color(0.78f, 0.47f, 0.47f, 0.12f));
        CreateActionKey(_actionStack.transform, "B", "Exit",
            new Color(1f, 0.67f, 0.19f, 1f), new Color(1f, 0.67f, 0.19f, 0.12f));
    }

    private void CreateKeycap(Transform parent, string key, string label, float width)
    {
        var cap = new GameObject(key + "_Cap");
        cap.transform.SetParent(parent, false);
        var capRect = cap.AddComponent<RectTransform>();
        capRect.sizeDelta = new Vector2(width, 36f);

        // key background
        var keyObj = new GameObject("Key");
        keyObj.transform.SetParent(cap.transform, false);
        var keyImg = keyObj.AddComponent<Image>();
        keyImg.color = new Color(1f, 0.67f, 0.19f, 0.12f);
        keyImg.raycastTarget = false;
        var keyRect = keyImg.rectTransform;
        keyRect.anchorMin = new Vector2(0.5f, 1f);
        keyRect.anchorMax = new Vector2(0.5f, 1f);
        keyRect.pivot = new Vector2(0.5f, 1f);
        keyRect.sizeDelta = new Vector2(width, 24f);
        keyRect.anchoredPosition = Vector2.zero;

        // rounded corners via sprite would be ideal, but for now outline works
        var outline = keyObj.AddComponent<Outline>();
        outline.effectColor = new Color(1f, 0.67f, 0.19f, 0.35f);
        outline.effectDistance = new Vector2(1f, 1f);

        // key text
        var keyText = new GameObject("KeyText");
        keyText.transform.SetParent(keyObj.transform, false);
        var tmp = keyText.AddComponent<TextMeshProUGUI>();
        tmp.text = key;
        tmp.fontSize = key.Length > 1 ? 9f : 12f;
        tmp.alignment = TextAlignmentOptions.Center;
        tmp.color = new Color(1f, 0.67f, 0.19f, 1f);
        tmp.fontStyle = FontStyles.Bold;
        tmp.raycastTarget = false;
        var tmpRect = tmp.rectTransform;
        tmpRect.anchorMin = Vector2.zero;
        tmpRect.anchorMax = Vector2.one;
        tmpRect.sizeDelta = Vector2.zero;

        // label text below
        var labelObj = new GameObject("Label");
        labelObj.transform.SetParent(cap.transform, false);
        var labelTmp = labelObj.AddComponent<TextMeshProUGUI>();
        labelTmp.text = label;
        labelTmp.fontSize = 8f;
        labelTmp.alignment = TextAlignmentOptions.Center;
        labelTmp.color = new Color(0.4f, 0.4f, 0.4f, 1f);
        labelTmp.raycastTarget = false;
        var labelRect = labelTmp.rectTransform;
        labelRect.anchorMin = new Vector2(0.5f, 0f);
        labelRect.anchorMax = new Vector2(0.5f, 0f);
        labelRect.pivot = new Vector2(0.5f, 0f);
        labelRect.sizeDelta = new Vector2(width + 10f, 12f);
        labelRect.anchoredPosition = Vector2.zero;
    }

    private void CreateActionKey(Transform parent, string key, string label, Color textColor, Color bgColor)
    {
        var row = new GameObject(key + "_Action");
        row.transform.SetParent(parent, false);
        var rowRect = row.AddComponent<RectTransform>();
        rowRect.sizeDelta = new Vector2(70f, 26f);

        var rowLayout = row.AddComponent<HorizontalLayoutGroup>();
        rowLayout.childAlignment = TextAnchor.MiddleRight;
        rowLayout.spacing = 6f;
        rowLayout.childControlWidth = false;
        rowLayout.childControlHeight = false;
        rowLayout.childForceExpandWidth = false;
        rowLayout.childForceExpandHeight = false;

        // label
        var labelObj = new GameObject("Label");
        labelObj.transform.SetParent(row.transform, false);
        var labelTmp = labelObj.AddComponent<TextMeshProUGUI>();
        labelTmp.text = label;
        labelTmp.fontSize = 8f;
        labelTmp.alignment = TextAlignmentOptions.MidlineRight;
        labelTmp.color = new Color(0.4f, 0.4f, 0.4f, 1f);
        labelTmp.raycastTarget = false;
        var labelRect = labelTmp.rectTransform;
        labelRect.sizeDelta = new Vector2(36f, 24f);

        // key box
        var keyObj = new GameObject("Key");
        keyObj.transform.SetParent(row.transform, false);
        var keyImg = keyObj.AddComponent<Image>();
        keyImg.color = bgColor;
        keyImg.raycastTarget = false;
        var keyRect = keyImg.rectTransform;
        keyRect.sizeDelta = new Vector2(key.Length > 1 ? 38f : 28f, 24f);

        var outline = keyObj.AddComponent<Outline>();
        outline.effectColor = new Color(textColor.r, textColor.g, textColor.b, 0.35f);
        outline.effectDistance = new Vector2(1f, 1f);

        var keyText = new GameObject("KeyText");
        keyText.transform.SetParent(keyObj.transform, false);
        var tmp = keyText.AddComponent<TextMeshProUGUI>();
        tmp.text = key;
        tmp.fontSize = key.Length > 1 ? 9f : 12f;
        tmp.alignment = TextAlignmentOptions.Center;
        tmp.color = textColor;
        tmp.fontStyle = FontStyles.Bold;
        tmp.raycastTarget = false;
        var tmpRect = tmp.rectTransform;
        tmpRect.anchorMin = Vector2.zero;
        tmpRect.anchorMax = Vector2.one;
        tmpRect.sizeDelta = Vector2.zero;
    }
}
