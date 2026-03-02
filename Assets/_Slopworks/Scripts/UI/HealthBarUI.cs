using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Health bar display using a filled Image. Subscribes to HealthComponent events.
/// </summary>
public class HealthBarUI : MonoBehaviour
{
    private Image _fillImage;
    private TextMeshProUGUI _healthText;
    private HealthComponent _health;

    public void Initialize(HealthComponent health)
    {
        _health = health;
        if (_health != null)
            _health.OnDamaged += _ => UpdateDisplay();
        UpdateDisplay();
    }

    public void Setup(Image fillImage, TextMeshProUGUI healthText)
    {
        _fillImage = fillImage;
        _healthText = healthText;
    }

    public void UpdateDisplay()
    {
        if (_health == null) return;

        float ratio = _health.CurrentHealth / _health.MaxHealth;

        if (_fillImage != null)
        {
            _fillImage.fillAmount = ratio;
            if (ratio > 0.5f) _fillImage.color = new Color(0.2f, 0.8f, 0.2f);
            else if (ratio > 0.2f) _fillImage.color = new Color(0.9f, 0.7f, 0.1f);
            else _fillImage.color = new Color(0.9f, 0.2f, 0.2f);
        }

        if (_healthText != null)
            _healthText.text = $"{Mathf.CeilToInt(_health.CurrentHealth)}/{Mathf.CeilToInt(_health.MaxHealth)}";
    }
}
