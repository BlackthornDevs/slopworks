using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Persistent visor HUD layout matching the approved v4 element map.
/// Creates all elements at runtime: top strip (badges + raid bar), compass,
/// frame status panel, vitals (health/shield/ammo), hotbar, equipment grid.
/// Purely visual placeholders — no game logic wired yet.
/// Call Build() once after the Canvas exists.
/// </summary>
public class VisorHUD : MonoBehaviour
{
    private static readonly Color Cyan = new(0f, 0.78f, 1f, 1f);
    private static readonly Color CyanHalf = new(0f, 0.78f, 1f, 0.5f);
    private static readonly Color CyanGhost = new(0f, 0.78f, 1f, 0.04f);

    private GameObject _root;
    private Image _healthFill;
    private Image _shieldFill;

    public void Build(Transform canvasTransform)
    {
        _root = new GameObject("VisorElements");
        _root.transform.SetParent(canvasTransform, false);
        var rt = _root.AddComponent<RectTransform>();
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.sizeDelta = Vector2.zero;

        BuildTopStrip(_root.transform);
        BuildCompass(_root.transform);
        BuildFrameStatus(_root.transform);
        BuildVitals(_root.transform);
        BuildHotbar(_root.transform);
        BuildEquipment(_root.transform);
    }

    public void SetVisible(bool visible)
    {
        if (_root != null) _root.SetActive(visible);
    }

    public void SetHealth(float normalized)
    {
        if (_healthFill != null) _healthFill.fillAmount = Mathf.Clamp01(normalized);
    }

    public void SetShield(float normalized)
    {
        if (_shieldFill != null) _shieldFill.fillAmount = Mathf.Clamp01(normalized);
    }

    // --- top strip: badge | raid bar | badge ---

    private void BuildTopStrip(Transform parent)
    {
        var strip = new GameObject("TopStrip");
        strip.transform.SetParent(parent, false);
        var rect = strip.AddComponent<RectTransform>();
        rect.anchorMin = new Vector2(0f, 1f);
        rect.anchorMax = new Vector2(1f, 1f);
        rect.pivot = new Vector2(0.5f, 1f);
        rect.anchoredPosition = new Vector2(0f, -6f);
        rect.sizeDelta = new Vector2(-20f, 18f);

        var layout = strip.AddComponent<HorizontalLayoutGroup>();
        layout.childAlignment = TextAnchor.MiddleCenter;
        layout.spacing = 10f;
        layout.childControlWidth = true;
        layout.childControlHeight = true;
        layout.childForceExpandWidth = false;
        layout.childForceExpandHeight = true;

        // badge left
        var bl = Img(strip.transform, "BadgeLeft", new Color(0.71f, 0.63f, 0.86f, 0.2f));
        bl.gameObject.AddComponent<LayoutElement>().preferredWidth = 90f;
        Txt(bl.transform, "Text", "ZONE A", 9f, new Color(0.71f, 0.63f, 0.86f, 1f), true);

        // raid bar
        var raid = Img(strip.transform, "RaidBar", Color.white);
        raid.sprite = MakeGradient(new Color(0.9f, 0.19f, 0.19f), new Color(0.94f, 0.5f, 0.19f));
        var rle = raid.gameObject.AddComponent<LayoutElement>();
        rle.flexibleWidth = 1f;
        rle.preferredWidth = 200f;
        Txt(raid.transform, "Text", "RAID STATUS", 9f, Color.white, true);

        // badge right
        var br = Img(strip.transform, "BadgeRight", new Color(0.63f, 0.71f, 0.86f, 0.2f));
        br.gameObject.AddComponent<LayoutElement>().preferredWidth = 90f;
        Txt(br.transform, "Text", "DAY 12", 9f, new Color(0.63f, 0.71f, 0.86f, 1f), true);
    }

    // --- compass ---

    private void BuildCompass(Transform parent)
    {
        var comp = new GameObject("Compass");
        comp.transform.SetParent(parent, false);
        var rect = comp.AddComponent<RectTransform>();
        rect.anchorMin = new Vector2(0.5f, 1f);
        rect.anchorMax = new Vector2(0.5f, 1f);
        rect.pivot = new Vector2(0.5f, 1f);
        rect.anchoredPosition = new Vector2(0f, -30f);
        rect.sizeDelta = new Vector2(200f, 16f);

        var layout = comp.AddComponent<HorizontalLayoutGroup>();
        layout.childAlignment = TextAnchor.MiddleCenter;
        layout.spacing = 2f;
        layout.childControlWidth = false;
        layout.childControlHeight = false;

        var purple = new Color(0.53f, 0.4f, 0.8f);
        var teal = new Color(0.27f, 0.8f, 0.8f);
        var blue = new Color(0.4f, 0.53f, 0.8f);

        TxtSized(comp.transform, "W", "W", 11f, purple, 14f);
        CompassBar(comp.transform, purple);
        CompassBar(comp.transform, blue);

        // diamond marker
        var dia = Img(comp.transform, "Diamond", new Color(0.4f, 0.87f, 0.4f));
        dia.rectTransform.sizeDelta = new Vector2(8f, 8f);
        dia.rectTransform.localRotation = Quaternion.Euler(0, 0, 45f);

        CompassBar(comp.transform, new Color(0.27f, 0.67f, 0.8f));
        CompassBar(comp.transform, teal);
        TxtSized(comp.transform, "N", "N", 11f, teal, 14f);
        CompassBar(comp.transform, teal);
        CompassBar(comp.transform, new Color(0.27f, 0.67f, 0.8f));
        CompassBar(comp.transform, blue);
        TxtSized(comp.transform, "E", "E", 11f, purple, 14f);
    }

    // --- frame status (sci-fi bordered panel) ---

    private void BuildFrameStatus(Transform parent)
    {
        var frame = new GameObject("FrameStatus");
        frame.transform.SetParent(parent, false);
        var rect = frame.AddComponent<RectTransform>();
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.zero;
        rect.pivot = Vector2.zero;
        rect.anchoredPosition = new Vector2(12f, 52f);
        rect.sizeDelta = new Vector2(240f, 100f);

        // background + border
        var bg = frame.AddComponent<Image>();
        bg.color = CyanGhost;
        bg.raycastTarget = false;
        var outline = frame.AddComponent<Outline>();
        outline.effectColor = CyanHalf;
        outline.effectDistance = new Vector2(2f, 2f);

        // corner accent — top-left horizontal
        var tlH = Img(frame.transform, "AccentTL_H", Cyan);
        tlH.rectTransform.sizeDelta = new Vector2(24f, 2f);
        tlH.rectTransform.anchorMin = new Vector2(0f, 1f);
        tlH.rectTransform.anchorMax = new Vector2(0f, 1f);
        tlH.rectTransform.pivot = new Vector2(0f, 0.5f);
        tlH.rectTransform.anchoredPosition = new Vector2(12f, 1f);

        // corner accent — top-left vertical
        var tlV = Img(frame.transform, "AccentTL_V", Cyan);
        tlV.rectTransform.sizeDelta = new Vector2(2f, 20f);
        tlV.rectTransform.anchorMin = new Vector2(0f, 1f);
        tlV.rectTransform.anchorMax = new Vector2(0f, 1f);
        tlV.rectTransform.pivot = new Vector2(0.5f, 1f);
        tlV.rectTransform.anchoredPosition = new Vector2(-1f, -12f);

        // corner accent — top-right horizontal
        var trH = Img(frame.transform, "AccentTR_H", Cyan);
        trH.rectTransform.sizeDelta = new Vector2(24f, 2f);
        trH.rectTransform.anchorMin = new Vector2(1f, 1f);
        trH.rectTransform.anchorMax = new Vector2(1f, 1f);
        trH.rectTransform.pivot = new Vector2(1f, 0.5f);
        trH.rectTransform.anchoredPosition = new Vector2(-12f, 1f);

        // content
        var label = Txt(frame.transform, "Label", "FACTORY STATUS", 10f, Cyan, false);
        label.rectTransform.anchorMin = new Vector2(0.5f, 0.5f);
        label.rectTransform.anchorMax = new Vector2(0.5f, 0.5f);
        label.rectTransform.anchoredPosition = new Vector2(0f, 8f);
        label.rectTransform.sizeDelta = new Vector2(200f, 20f);

        var sub = Txt(frame.transform, "Sub", "machines: 0 | belts: 0 | power: 0", 8f,
            new Color(0.4f, 0.4f, 0.4f, 1f), false);
        sub.rectTransform.anchorMin = new Vector2(0.5f, 0.5f);
        sub.rectTransform.anchorMax = new Vector2(0.5f, 0.5f);
        sub.rectTransform.anchoredPosition = new Vector2(0f, -8f);
        sub.rectTransform.sizeDelta = new Vector2(220f, 16f);
    }

    // --- vitals: ammo + health + shield ---

    private void BuildVitals(Transform parent)
    {
        var vitals = new GameObject("Vitals");
        vitals.transform.SetParent(parent, false);
        var rect = vitals.AddComponent<RectTransform>();
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.zero;
        rect.pivot = Vector2.zero;
        rect.anchoredPosition = new Vector2(12f, 12f);
        rect.sizeDelta = new Vector2(180f, 28f);

        // ammo indicator square
        var ammo = Img(vitals.transform, "Ammo", new Color(0.67f, 1f, 0.13f, 1f));
        ammo.rectTransform.anchorMin = Vector2.zero;
        ammo.rectTransform.anchorMax = Vector2.zero;
        ammo.rectTransform.pivot = Vector2.zero;
        ammo.rectTransform.sizeDelta = new Vector2(20f, 20f);
        ammo.rectTransform.anchoredPosition = new Vector2(0f, 4f);

        // health bar
        PlaceBar(vitals.transform, "HealthBg", new Color(0.15f, 0.1f, 0.1f, 0.6f),
            new Vector2(150f, 9f), new Vector2(26f, 14f));
        _healthFill = PlaceBar(vitals.transform, "HealthFill", Color.white,
            new Vector2(150f, 9f), new Vector2(26f, 14f));
        _healthFill.sprite = MakeGradient(
            new Color(1f, 0.27f, 0.67f), new Color(0.93f, 0.2f, 0.2f),
            new Color(1f, 0.53f, 0.2f), new Color(1f, 0.87f, 0.27f));
        _healthFill.type = Image.Type.Filled;
        _healthFill.fillMethod = Image.FillMethod.Horizontal;
        _healthFill.fillAmount = 0.82f;

        // shield bar
        PlaceBar(vitals.transform, "ShieldBg", new Color(0.1f, 0.1f, 0.15f, 0.6f),
            new Vector2(120f, 7f), new Vector2(26f, 2f));
        _shieldFill = PlaceBar(vitals.transform, "ShieldFill", Color.white,
            new Vector2(120f, 7f), new Vector2(26f, 2f));
        _shieldFill.sprite = MakeGradient(
            new Color(0.4f, 0.2f, 0.8f), new Color(0.27f, 0.53f, 0.8f),
            new Color(0.13f, 0.8f, 0.8f));
        _shieldFill.type = Image.Type.Filled;
        _shieldFill.fillMethod = Image.FillMethod.Horizontal;
        _shieldFill.fillAmount = 0.65f;
    }

    // --- hotbar: tool selector + 10 slots ---

    private void BuildHotbar(Transform parent)
    {
        var bar = new GameObject("Hotbar");
        bar.transform.SetParent(parent, false);
        var rect = bar.AddComponent<RectTransform>();
        rect.anchorMin = new Vector2(0.5f, 0f);
        rect.anchorMax = new Vector2(0.5f, 0f);
        rect.pivot = new Vector2(0.5f, 0f);
        rect.anchoredPosition = new Vector2(0f, 12f);
        rect.sizeDelta = new Vector2(330f, 30f);

        var layout = bar.AddComponent<HorizontalLayoutGroup>();
        layout.childAlignment = TextAnchor.MiddleCenter;
        layout.spacing = 3f;
        layout.childControlWidth = false;
        layout.childControlHeight = false;

        // tool selector circle
        var sel = Img(bar.transform, "ToolSelector", Color.clear);
        sel.rectTransform.sizeDelta = new Vector2(22f, 22f);
        sel.gameObject.AddComponent<Outline>().effectColor = new Color(0.86f, 0.39f, 0.86f, 0.4f);
        var selT = Txt(sel.transform, "Icon", "@", 9f, new Color(0.86f, 0.39f, 0.86f, 1f), true);

        // 10 hotbar slots
        for (int i = 0; i < 10; i++)
        {
            var slot = Img(bar.transform, "Slot" + i, new Color(0.47f, 0.47f, 0.55f, 0.4f));
            slot.rectTransform.sizeDelta = new Vector2(26f, 26f);
            slot.gameObject.AddComponent<Outline>().effectColor = new Color(0.5f, 0.5f, 0.6f, 0.3f);
        }
    }

    // --- equipment: 5x2 loadout grid + gear button ---

    private void BuildEquipment(Transform parent)
    {
        var equip = new GameObject("Equipment");
        equip.transform.SetParent(parent, false);
        var rect = equip.AddComponent<RectTransform>();
        rect.anchorMin = new Vector2(1f, 0f);
        rect.anchorMax = new Vector2(1f, 0f);
        rect.pivot = new Vector2(1f, 0f);
        rect.anchoredPosition = new Vector2(-12f, 12f);
        rect.sizeDelta = new Vector2(160f, 48f);

        var layout = equip.AddComponent<HorizontalLayoutGroup>();
        layout.childAlignment = TextAnchor.MiddleRight;
        layout.spacing = 6f;
        layout.childControlWidth = false;
        layout.childControlHeight = false;

        // 5x2 loadout grid
        var grid = new GameObject("LoadoutGrid");
        grid.transform.SetParent(equip.transform, false);
        grid.AddComponent<RectTransform>().sizeDelta = new Vector2(110f, 44f);
        var gl = grid.AddComponent<GridLayoutGroup>();
        gl.cellSize = new Vector2(20f, 20f);
        gl.spacing = new Vector2(2f, 2f);
        gl.startCorner = GridLayoutGroup.Corner.UpperLeft;
        gl.startAxis = GridLayoutGroup.Axis.Horizontal;
        gl.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
        gl.constraintCount = 5;
        gl.childAlignment = TextAnchor.MiddleRight;

        for (int i = 0; i < 10; i++)
        {
            var cell = Img(grid.transform, "L" + i, new Color(0.31f, 0.24f, 0.2f, 0.8f));
            cell.gameObject.AddComponent<Outline>().effectColor = new Color(0.39f, 0.31f, 0.27f, 0.5f);
        }

        // gear button
        var gear = Img(equip.transform, "GearBtn", new Color(0.47f, 0.47f, 0.55f, 0.3f));
        gear.rectTransform.sizeDelta = new Vector2(32f, 44f);
        gear.gameObject.AddComponent<Outline>().effectColor = new Color(0.47f, 0.47f, 0.55f, 0.4f);
        Txt(gear.transform, "Label", "GEAR", 9f, new Color(0.67f, 0.67f, 0.67f, 1f), true);
    }

    // --- helpers ---

    private static Image Img(Transform parent, string name, Color color)
    {
        var obj = new GameObject(name);
        obj.transform.SetParent(parent, false);
        var img = obj.AddComponent<Image>();
        img.color = color;
        img.raycastTarget = false;
        return img;
    }

    private static TextMeshProUGUI Txt(Transform parent, string name, string text,
        float size, Color color, bool fill)
    {
        var obj = new GameObject(name);
        obj.transform.SetParent(parent, false);
        var tmp = obj.AddComponent<TextMeshProUGUI>();
        tmp.text = text;
        tmp.fontSize = size;
        tmp.color = color;
        tmp.alignment = TextAlignmentOptions.Center;
        tmp.raycastTarget = false;
        if (fill)
        {
            tmp.rectTransform.anchorMin = Vector2.zero;
            tmp.rectTransform.anchorMax = Vector2.one;
            tmp.rectTransform.sizeDelta = Vector2.zero;
        }
        return tmp;
    }

    private static TextMeshProUGUI TxtSized(Transform parent, string name, string text,
        float size, Color color, float width)
    {
        var tmp = Txt(parent, name, text, size, color, false);
        tmp.fontStyle = FontStyles.Bold;
        tmp.rectTransform.sizeDelta = new Vector2(width, 16f);
        return tmp;
    }

    private static void CompassBar(Transform parent, Color color)
    {
        var bar = Img(parent, "Bar", color);
        bar.rectTransform.sizeDelta = new Vector2(16f, 4f);
    }

    private static Image PlaceBar(Transform parent, string name, Color color,
        Vector2 size, Vector2 pos)
    {
        var img = Img(parent, name, color);
        img.rectTransform.anchorMin = Vector2.zero;
        img.rectTransform.anchorMax = Vector2.zero;
        img.rectTransform.pivot = Vector2.zero;
        img.rectTransform.sizeDelta = size;
        img.rectTransform.anchoredPosition = pos;
        return img;
    }

    private static Sprite MakeGradient(params Color[] colors)
    {
        const int w = 64;
        var tex = new Texture2D(w, 1, TextureFormat.RGBA32, false);
        tex.wrapMode = TextureWrapMode.Clamp;
        int segs = colors.Length - 1;
        for (int i = 0; i < w; i++)
        {
            float t = i / (float)(w - 1);
            float seg = t * segs;
            int idx = Mathf.Min((int)seg, segs - 1);
            tex.SetPixel(i, 0, Color.Lerp(colors[idx], colors[idx + 1], seg - idx));
        }
        tex.Apply();
        return Sprite.Create(tex, new Rect(0, 0, w, 1), new Vector2(0.5f, 0.5f));
    }
}
