using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Consolidated HUD. Creates all elements at runtime on the Canvas.
/// Supports both serialized references (scene-based setup) and runtime Initialize().
/// Features: health bar + text, ammo, wave status, damage flash, crosshair,
/// interaction prompt, build mode indicator, hotbar with page support.
/// </summary>
public class PlayerHUD : MonoBehaviour
{
    [Header("Player References (optional, can use Initialize instead)")]
    [SerializeField] private HealthBehaviour _playerHealthBehaviour;
    [SerializeField] private WeaponBehaviour _playerWeaponBehaviour;
    [SerializeField] private WaveControllerBehaviour _waveControllerBehaviour;
    [SerializeField] private CameraShake _cameraShake;
    [SerializeField] private PlayerInventory _playerInventory;
    [SerializeField] private Camera _playerCamera;

    // -- Joe's combat HUD --
    private TextMeshProUGUI _ammoText;
    private TextMeshProUGUI _waveText;
    private Image _damageFlash;

    private HealthComponent _playerHealth;
    private WeaponController _playerWeapon;
    private WaveController _waveController;

    private float _damageFlashAlpha;
    private const float FlashFadeSpeed = 3f;
    private const float FlashIntensity = 0.4f;

    // -- Kevin's additions --
    private HealthBarUI _healthBar;
    private InteractionPromptUI _interactionPrompt;
    private Image _crosshairImage;
    private TextMeshProUGUI _buildModeText;
    private TextMeshProUGUI _waveWarningText;
    private TextMeshProUGUI _buildingStatusText;
    private HotbarSlotUI[] _hotbarSlots;
    private GameObject _hotbarContainer;
    private TextMeshProUGUI _hotbarPageLabel;

    // -- Hotbar pages --
    private HotbarPage[] _pages;
    private int _currentPageIndex;

    /// <summary>
    /// Fired when the active hotbar page changes.
    /// int = new page index.
    /// </summary>
    public event Action<int> OnPageChanged;

    /// <summary>
    /// Fired when a slot is pressed on a non-inventory page.
    /// (pageIndex, slotIndex, entryId)
    /// </summary>
    public event Action<int, int, string> OnBuildToolSelected;

    public int CurrentPageIndex => _currentPageIndex;
    public HotbarSlotUI[] HotbarSlots => _hotbarSlots;

    private void Start()
    {
        CreateUIElements();
        WireFromSerializedRefs();
    }

    /// <summary>
    /// Runtime initialization for playtest bootstrappers.
    /// Call after Start has run (use a coroutine yield return null).
    /// </summary>
    public void Initialize(HealthComponent playerHealth, WeaponController weapon,
                           CameraShake cameraShake, WaveController waveController)
    {
        _playerHealth = playerHealth;
        _playerWeapon = weapon;
        _cameraShake = cameraShake;

        if (_playerHealth != null)
        {
            _playerHealth.OnDamaged += OnPlayerDamaged;
            _playerHealth.OnDeath += OnPlayerDeath;
            _healthBar?.Initialize(_playerHealth);
            UpdateHealthDisplay();
        }

        if (waveController != null)
        {
            _waveController = waveController;
            _waveController.OnWaveStarted += OnWaveStarted;
            _waveController.OnWaveEnded += OnWaveEnded;
        }
    }

    /// <summary>
    /// Wire inventory and camera for hotbar + interaction prompt.
    /// </summary>
    public void InitializeInventory(PlayerInventory inventory, Camera cam)
    {
        _playerInventory = inventory;
        _playerCamera = cam;

        if (_playerCamera != null && _interactionPrompt != null)
        {
            var promptText = _interactionPrompt.GetComponentInChildren<TextMeshProUGUI>();
            _interactionPrompt.Setup(promptText, _playerCamera);
        }

        if (_playerInventory != null && _hotbarSlots != null)
        {
            for (int i = 0; i < _hotbarSlots.Length; i++)
                _hotbarSlots[i].Bind(_playerInventory, i);
        }
    }

    /// <summary>
    /// Register hotbar pages. Page 0 is typically inventory items.
    /// </summary>
    public void SetPages(HotbarPage[] pages)
    {
        _pages = pages;
        _currentPageIndex = 0;
        RefreshHotbarPage();
    }

    /// <summary>
    /// Switch to the next page (wraps around).
    /// </summary>
    public void TogglePage()
    {
        if (_pages == null || _pages.Length < 2) return;
        _currentPageIndex = (_currentPageIndex + 1) % _pages.Length;
        RefreshHotbarPage();
        OnPageChanged?.Invoke(_currentPageIndex);
        Debug.Log($"hotbar: switched to page {_currentPageIndex} ({_pages[_currentPageIndex].PageName})");
    }

    /// <summary>
    /// Force switch to a specific page index.
    /// </summary>
    public void SetPage(int index)
    {
        if (_pages == null || index < 0 || index >= _pages.Length) return;
        if (_currentPageIndex == index) return;
        _currentPageIndex = index;
        RefreshHotbarPage();
        OnPageChanged?.Invoke(_currentPageIndex);
    }

    /// <summary>
    /// Handle a digit key press (0-based slot index).
    /// On page 0 (items): selects inventory hotbar slot.
    /// On other pages: fires OnBuildToolSelected.
    /// </summary>
    public void OnSlotPressed(int slotIndex)
    {
        if (slotIndex < 0 || slotIndex >= PlayerInventory.HotbarSlots) return;

        if (_currentPageIndex == 0)
        {
            _playerInventory?.SelectHotbarSlot(slotIndex);
        }
        else if (_pages != null && _currentPageIndex < _pages.Length)
        {
            var page = _pages[_currentPageIndex];
            if (slotIndex < page.Entries.Length && page.Entries[slotIndex].Id != null)
            {
                OnBuildToolSelected?.Invoke(_currentPageIndex, slotIndex, page.Entries[slotIndex].Id);
            }
        }

        UpdateHotbarSelection(slotIndex);
    }

    public void SetBuildModeVisible(bool visible)
    {
        if (_buildModeText != null)
            _buildModeText.gameObject.SetActive(visible);
    }

    public void SetBuildingStatus(string text)
    {
        if (_buildingStatusText == null) return;

        if (string.IsNullOrEmpty(text))
        {
            _buildingStatusText.gameObject.SetActive(false);
        }
        else
        {
            _buildingStatusText.text = text;
            _buildingStatusText.gameObject.SetActive(true);
        }
    }

    public void ShowWaveWarning(string message)
    {
        if (_waveWarningText != null)
        {
            _waveWarningText.text = message;
            _waveWarningText.gameObject.SetActive(true);
            CancelInvoke(nameof(HideWaveWarning));
            Invoke(nameof(HideWaveWarning), 3f);
        }
    }

    private void OnDisable()
    {
        if (_playerHealth != null)
        {
            _playerHealth.OnDamaged -= OnPlayerDamaged;
            _playerHealth.OnDeath -= OnPlayerDeath;
        }

        if (_waveController != null)
        {
            _waveController.OnWaveStarted -= OnWaveStarted;
            _waveController.OnWaveEnded -= OnWaveEnded;
        }
    }

    private void Update()
    {
        UpdateAmmoDisplay();
        UpdateDamageFlash();
        _healthBar?.UpdateDisplay();
        UpdateHotbarFromInventory();
    }

    // -- Wire serialized references (editor-assigned, Joe's workflow) --

    private void WireFromSerializedRefs()
    {
        if (_playerHealthBehaviour != null)
            _playerHealth = _playerHealthBehaviour.Health;
        if (_playerWeaponBehaviour != null)
            _playerWeapon = _playerWeaponBehaviour.Weapon;
        if (_waveControllerBehaviour != null)
            _waveController = _waveControllerBehaviour.Controller;

        if (_playerHealth != null)
        {
            _playerHealth.OnDamaged += OnPlayerDamaged;
            _playerHealth.OnDeath += OnPlayerDeath;
            _healthBar?.Initialize(_playerHealth);
            UpdateHealthDisplay();
        }

        if (_waveController != null)
        {
            _waveController.OnWaveStarted += OnWaveStarted;
            _waveController.OnWaveEnded += OnWaveEnded;
        }

        if (_playerCamera != null && _interactionPrompt != null)
        {
            var promptText = _interactionPrompt.GetComponentInChildren<TextMeshProUGUI>();
            _interactionPrompt.Setup(promptText, _playerCamera);
        }

        if (_playerInventory != null && _hotbarSlots != null)
        {
            for (int i = 0; i < _hotbarSlots.Length; i++)
                _hotbarSlots[i].Bind(_playerInventory, i);
        }
    }

    // -- UI creation --

    private void CreateUIElements()
    {
        var canvas = GetComponent<Canvas>();
        if (canvas == null)
        {
            Debug.LogError("PlayerHUD must be on a Canvas");
            enabled = false;
            return;
        }

        CreateHealthBar();
        CreateAmmoText();
        CreateWaveText();
        CreateInteractionPrompt();
        CreateBuildModeIndicator();
        CreateWaveWarning();
        CreateBuildingStatus();
        CreateDamageFlash();
    }

    private void CreateCrosshair()
    {
        var obj = new GameObject("Crosshair");
        obj.transform.SetParent(transform, false);
        _crosshairImage = obj.AddComponent<Image>();
        _crosshairImage.color = new Color(1f, 1f, 1f, 0.7f);
        _crosshairImage.raycastTarget = false;
        var rect = _crosshairImage.rectTransform;
        rect.anchorMin = new Vector2(0.5f, 0.5f);
        rect.anchorMax = new Vector2(0.5f, 0.5f);
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.sizeDelta = new Vector2(4, 4);
        rect.anchoredPosition = Vector2.zero;
    }

    private void CreateHealthBar()
    {
        // Background
        var bgObj = new GameObject("HealthBarBG");
        bgObj.transform.SetParent(transform, false);
        var bgImage = bgObj.AddComponent<Image>();
        bgImage.color = new Color(0.2f, 0.2f, 0.2f, 0.8f);
        bgImage.raycastTarget = false;
        var bgRect = bgImage.rectTransform;
        bgRect.anchorMin = new Vector2(0, 1);
        bgRect.anchorMax = new Vector2(0, 1);
        bgRect.pivot = new Vector2(0, 1);
        bgRect.sizeDelta = new Vector2(200, 24);
        bgRect.anchoredPosition = new Vector2(16, -16);

        // Fill
        var fillObj = new GameObject("HealthBarFill");
        fillObj.transform.SetParent(bgObj.transform, false);
        var fillImage = fillObj.AddComponent<Image>();
        fillImage.color = new Color(0.2f, 0.8f, 0.2f);
        fillImage.type = Image.Type.Filled;
        fillImage.fillMethod = Image.FillMethod.Horizontal;
        fillImage.fillAmount = 1f;
        fillImage.raycastTarget = false;
        var fillRect = fillImage.rectTransform;
        fillRect.anchorMin = Vector2.zero;
        fillRect.anchorMax = Vector2.one;
        fillRect.offsetMin = new Vector2(2, 2);
        fillRect.offsetMax = new Vector2(-2, -2);

        // Text overlay on health bar
        var textObj = new GameObject("HealthText");
        textObj.transform.SetParent(bgObj.transform, false);
        var healthText = textObj.AddComponent<TextMeshProUGUI>();
        healthText.fontSize = 14;
        healthText.alignment = TextAlignmentOptions.Center;
        healthText.color = Color.white;
        healthText.raycastTarget = false;
        var textRect = healthText.rectTransform;
        textRect.anchorMin = Vector2.zero;
        textRect.anchorMax = Vector2.one;
        textRect.offsetMin = Vector2.zero;
        textRect.offsetMax = Vector2.zero;

        _healthBar = bgObj.AddComponent<HealthBarUI>();
        _healthBar.Setup(fillImage, healthText);
    }

    private void CreateAmmoText()
    {
        _ammoText = CreateText("AmmoText", new Vector2(-80, -40),
            TextAlignmentOptions.TopRight, 22);
        _ammoText.text = "";
    }

    private void CreateWaveText()
    {
        _waveText = CreateText("WaveText", new Vector2(0, -40),
            TextAlignmentOptions.Top, 20);
        _waveText.text = "";
    }

    private void CreateInteractionPrompt()
    {
        var obj = new GameObject("InteractionPrompt");
        obj.transform.SetParent(transform, false);

        var text = obj.AddComponent<TextMeshProUGUI>();
        text.fontSize = 16;
        text.alignment = TextAlignmentOptions.Center;
        text.color = Color.white;
        text.raycastTarget = false;

        var rect = text.rectTransform;
        rect.anchorMin = new Vector2(0.5f, 0.5f);
        rect.anchorMax = new Vector2(0.5f, 0.5f);
        rect.pivot = new Vector2(0.5f, 1f);
        rect.sizeDelta = new Vector2(400, 30);
        rect.anchoredPosition = new Vector2(0, -30);

        obj.SetActive(false);

        _interactionPrompt = obj.AddComponent<InteractionPromptUI>();
        _interactionPrompt.Setup(text, _playerCamera);
    }

    private void CreateBuildModeIndicator()
    {
        var obj = new GameObject("BuildModeIndicator");
        obj.transform.SetParent(transform, false);

        _buildModeText = obj.AddComponent<TextMeshProUGUI>();
        _buildModeText.fontSize = 18;
        _buildModeText.alignment = TextAlignmentOptions.Center;
        _buildModeText.color = new Color(1f, 0.9f, 0.3f);
        _buildModeText.text = "BUILD MODE";
        _buildModeText.raycastTarget = false;

        var rect = _buildModeText.rectTransform;
        rect.anchorMin = new Vector2(0.5f, 1);
        rect.anchorMax = new Vector2(0.5f, 1);
        rect.pivot = new Vector2(0.5f, 1);
        rect.sizeDelta = new Vector2(200, 30);
        rect.anchoredPosition = new Vector2(0, -16);

        obj.SetActive(false);
    }

    private void CreateWaveWarning()
    {
        var obj = new GameObject("WaveWarning");
        obj.transform.SetParent(transform, false);

        _waveWarningText = obj.AddComponent<TextMeshProUGUI>();
        _waveWarningText.fontSize = 28;
        _waveWarningText.alignment = TextAlignmentOptions.Center;
        _waveWarningText.color = new Color(1f, 0.3f, 0.3f);
        _waveWarningText.raycastTarget = false;

        var rect = _waveWarningText.rectTransform;
        rect.anchorMin = new Vector2(0.5f, 1);
        rect.anchorMax = new Vector2(0.5f, 1);
        rect.pivot = new Vector2(0.5f, 1);
        rect.sizeDelta = new Vector2(600, 40);
        rect.anchoredPosition = new Vector2(0, -60);

        obj.SetActive(false);
    }

    private void CreateBuildingStatus()
    {
        _buildingStatusText = CreateText("BuildingStatus", new Vector2(16, -46),
            TextAlignmentOptions.TopLeft, 14);
        _buildingStatusText.color = new Color(0.8f, 0.9f, 1f);
        _buildingStatusText.text = "";
        _buildingStatusText.gameObject.SetActive(false);
    }

    private void CreateHotbar()
    {
        _hotbarContainer = new GameObject("HotbarContainer");
        _hotbarContainer.transform.SetParent(transform, false);

        var containerRect = _hotbarContainer.AddComponent<RectTransform>();
        containerRect.anchorMin = new Vector2(0.5f, 0);
        containerRect.anchorMax = new Vector2(0.5f, 0);
        containerRect.pivot = new Vector2(0.5f, 0);
        containerRect.sizeDelta = new Vector2(PlayerInventory.HotbarSlots * 56, 56);
        containerRect.anchoredPosition = new Vector2(0, 16);

        var layout = _hotbarContainer.AddComponent<HorizontalLayoutGroup>();
        layout.spacing = 4;
        layout.childAlignment = TextAnchor.MiddleCenter;
        layout.childControlWidth = true;
        layout.childControlHeight = true;
        layout.childForceExpandWidth = true;
        layout.childForceExpandHeight = true;

        _hotbarSlots = new HotbarSlotUI[PlayerInventory.HotbarSlots];
        for (int i = 0; i < PlayerInventory.HotbarSlots; i++)
            _hotbarSlots[i] = HotbarSlotUI.Create(_hotbarContainer.transform, i);

        // Page label below hotbar
        var labelObj = new GameObject("HotbarPageLabel");
        labelObj.transform.SetParent(transform, false);

        _hotbarPageLabel = labelObj.AddComponent<TextMeshProUGUI>();
        _hotbarPageLabel.fontSize = 12;
        _hotbarPageLabel.alignment = TextAlignmentOptions.Center;
        _hotbarPageLabel.color = new Color(1f, 1f, 1f, 0.6f);
        _hotbarPageLabel.text = "ITEMS";
        _hotbarPageLabel.raycastTarget = false;

        var labelRect = _hotbarPageLabel.rectTransform;
        labelRect.anchorMin = new Vector2(0.5f, 0);
        labelRect.anchorMax = new Vector2(0.5f, 0);
        labelRect.pivot = new Vector2(0.5f, 0);
        labelRect.sizeDelta = new Vector2(100, 20);
        labelRect.anchoredPosition = new Vector2(0, 74);
    }

    private void CreateDamageFlash()
    {
        var flashObj = new GameObject("DamageFlash");
        flashObj.transform.SetParent(transform, false);
        _damageFlash = flashObj.AddComponent<Image>();
        _damageFlash.color = new Color(0.8f, 0f, 0f, 0f);
        _damageFlash.raycastTarget = false;
        var flashRect = _damageFlash.rectTransform;
        flashRect.anchorMin = Vector2.zero;
        flashRect.anchorMax = Vector2.one;
        flashRect.offsetMin = Vector2.zero;
        flashRect.offsetMax = Vector2.zero;
    }

    private TextMeshProUGUI CreateText(string name, Vector2 position,
        TextAlignmentOptions alignment, int fontSize)
    {
        var obj = new GameObject(name);
        obj.transform.SetParent(transform, false);
        var text = obj.AddComponent<TextMeshProUGUI>();
        text.alignment = alignment;
        text.fontSize = fontSize;
        text.color = Color.white;
        text.textWrappingMode = TextWrappingModes.NoWrap;
        text.raycastTarget = false;

        var rect = text.rectTransform;
        rect.sizeDelta = new Vector2(300, 40);

        if (alignment == TextAlignmentOptions.TopLeft)
        {
            rect.anchorMin = new Vector2(0, 1);
            rect.anchorMax = new Vector2(0, 1);
            rect.pivot = new Vector2(0, 1);
        }
        else if (alignment == TextAlignmentOptions.TopRight)
        {
            rect.anchorMin = new Vector2(1, 1);
            rect.anchorMax = new Vector2(1, 1);
            rect.pivot = new Vector2(1, 1);
        }
        else
        {
            rect.anchorMin = new Vector2(0.5f, 1);
            rect.anchorMax = new Vector2(0.5f, 1);
            rect.pivot = new Vector2(0.5f, 1);
        }

        rect.anchoredPosition = position;
        return text;
    }

    // -- Hotbar page management --

    private void RefreshHotbarPage()
    {
        if (_pages == null || _hotbarSlots == null) return;

        var page = _pages[_currentPageIndex];

        if (_hotbarPageLabel != null)
            _hotbarPageLabel.text = page.PageName.ToUpper();

        if (_currentPageIndex == 0)
        {
            // Items page: rebind to inventory
            if (_playerInventory != null)
            {
                for (int i = 0; i < _hotbarSlots.Length; i++)
                    _hotbarSlots[i].Bind(_playerInventory, i);
            }
        }
        else
        {
            // Non-inventory page: show entries via SetEntry
            for (int i = 0; i < _hotbarSlots.Length; i++)
            {
                if (i < page.Entries.Length && page.Entries[i].Id != null)
                    _hotbarSlots[i].SetEntry(page.Entries[i].DisplayName, page.Entries[i].Color);
                else
                    _hotbarSlots[i].SetEntry(null, Color.clear);
            }
        }
    }

    private void UpdateHotbarFromInventory()
    {
        if (_playerInventory == null || _hotbarSlots == null) return;
        if (_currentPageIndex != 0) return;

        for (int i = 0; i < _hotbarSlots.Length; i++)
            _hotbarSlots[i].SetSelected(i == _playerInventory.SelectedHotbarIndex);
    }

    private void UpdateHotbarSelection(int selectedSlot)
    {
        if (_hotbarSlots == null) return;
        for (int i = 0; i < _hotbarSlots.Length; i++)
            _hotbarSlots[i].SetSelected(i == selectedSlot);
    }

    // -- Health display (from Joe's original) --

    private void UpdateHealthDisplay()
    {
        _healthBar?.UpdateDisplay();
    }

    // -- Ammo display --

    private void UpdateAmmoDisplay()
    {
        if (_playerWeapon == null || _ammoText == null) return;

        if (_playerWeapon.IsReloading)
            _ammoText.text = "RELOADING";
        else
            _ammoText.text = _playerWeapon.CurrentAmmo.ToString();
    }

    // -- Damage flash --

    private void UpdateDamageFlash()
    {
        if (_damageFlashAlpha <= 0f) return;

        _damageFlashAlpha -= FlashFadeSpeed * Time.deltaTime;
        if (_damageFlashAlpha < 0f) _damageFlashAlpha = 0f;

        _damageFlash.color = new Color(0.8f, 0f, 0f, _damageFlashAlpha);
    }

    // -- Event handlers --

    private void OnPlayerDamaged(DamageData damage)
    {
        UpdateHealthDisplay();
        _damageFlashAlpha = FlashIntensity;

        if (_cameraShake != null)
        {
            float intensity = Mathf.Clamp01(damage.amount / 30f);
            _cameraShake.Shake(intensity);
        }
    }

    private void OnPlayerDeath()
    {
        _damageFlashAlpha = 0.6f;
    }

    private void OnWaveStarted()
    {
        if (_waveController == null || _waveText == null) return;
        _waveText.text = "wave " + (_waveController.CurrentWave + 1) +
                         " -- " + _waveController.EnemiesRemaining + " enemies";
    }

    private void OnWaveEnded()
    {
        if (_waveController == null || _waveText == null) return;
        _waveText.text = "wave " + (_waveController.CurrentWave + 1) + " cleared";
    }

    private void HideWaveWarning()
    {
        if (_waveWarningText != null)
            _waveWarningText.gameObject.SetActive(false);
    }
}
