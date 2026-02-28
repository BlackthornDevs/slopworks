using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class PlayerHUD : MonoBehaviour
{
    [SerializeField] private HealthBehaviour _playerHealthBehaviour;
    [SerializeField] private WeaponBehaviour _playerWeaponBehaviour;
    [SerializeField] private WaveControllerBehaviour _waveControllerBehaviour;
    [SerializeField] private CameraShake _cameraShake;

    private TextMeshProUGUI _healthText;
    private TextMeshProUGUI _ammoText;
    private TextMeshProUGUI _waveText;
    private Image _damageFlash;

    private HealthComponent _playerHealth;
    private WeaponController _playerWeapon;
    private WaveController _waveController;

    private float _damageFlashAlpha;
    private const float FLASH_FADE_SPEED = 3f;
    private const float FLASH_INTENSITY = 0.4f;

    private void Start()
    {
        CreateUIElements();

        // wire from serialized references (editor-assigned)
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
            UpdateHealthDisplay();
        }

        if (_waveController != null)
        {
            _waveController.OnWaveStarted += OnWaveStarted;
            _waveController.OnWaveEnded += OnWaveEnded;
        }
    }

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
            UpdateHealthDisplay();
        }

        if (waveController != null)
        {
            _waveController = waveController;
            _waveController.OnWaveStarted += OnWaveStarted;
            _waveController.OnWaveEnded += OnWaveEnded;
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
    }

    private void CreateUIElements()
    {
        var canvas = GetComponent<Canvas>();
        if (canvas == null)
        {
            Debug.LogError("PlayerHUD must be on a Canvas");
            enabled = false;
            return;
        }

        _healthText = CreateText("HealthText", new Vector2(80, -40),
            TextAlignmentOptions.TopLeft, 22);
        _healthText.text = "HP: --/--";

        _ammoText = CreateText("AmmoText", new Vector2(-80, -40),
            TextAlignmentOptions.TopRight, 22);
        _ammoText.text = "";

        _waveText = CreateText("WaveText", new Vector2(0, -40),
            TextAlignmentOptions.Top, 20);
        _waveText.text = "";

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

    private void UpdateHealthDisplay()
    {
        if (_playerHealth == null || _healthText == null) return;
        _healthText.text = "HP: " + Mathf.CeilToInt(_playerHealth.CurrentHealth) +
                           "/" + Mathf.CeilToInt(_playerHealth.MaxHealth);

        float ratio = _playerHealth.CurrentHealth / _playerHealth.MaxHealth;
        if (ratio > 0.5f)
            _healthText.color = Color.white;
        else if (ratio > 0.2f)
            _healthText.color = new Color(1f, 0.8f, 0.2f);
        else
            _healthText.color = new Color(1f, 0.3f, 0.3f);
    }

    private void UpdateAmmoDisplay()
    {
        if (_playerWeapon == null) return;

        if (_playerWeapon.IsReloading)
            _ammoText.text = "RELOADING";
        else
            _ammoText.text = _playerWeapon.CurrentAmmo.ToString();
    }

    private void UpdateDamageFlash()
    {
        if (_damageFlashAlpha <= 0f) return;

        _damageFlashAlpha -= FLASH_FADE_SPEED * Time.deltaTime;
        if (_damageFlashAlpha < 0f) _damageFlashAlpha = 0f;

        _damageFlash.color = new Color(0.8f, 0f, 0f, _damageFlashAlpha);
    }

    private void OnPlayerDamaged(DamageData damage)
    {
        UpdateHealthDisplay();
        _damageFlashAlpha = FLASH_INTENSITY;

        if (_cameraShake != null)
        {
            float intensity = Mathf.Clamp01(damage.amount / 30f);
            _cameraShake.Shake(intensity);
        }
    }

    private void OnPlayerDeath()
    {
        _healthText.text = "DEAD";
        _healthText.color = Color.red;
        _damageFlashAlpha = 0.6f;
    }

    private void OnWaveStarted()
    {
        if (_waveController == null) return;
        _waveText.text = "wave " + (_waveController.CurrentWave + 1) +
                         " — " + _waveController.EnemiesRemaining + " enemies";
    }

    private void OnWaveEnded()
    {
        if (_waveController == null) return;
        _waveText.text = "wave " + (_waveController.CurrentWave + 1) + " cleared";
    }
}
