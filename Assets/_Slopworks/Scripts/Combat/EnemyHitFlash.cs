using System.Collections;
using UnityEngine;

public class EnemyHitFlash : MonoBehaviour
{
    [SerializeField] private float _flashDuration = 0.08f;

    private Renderer _renderer;
    private HealthBehaviour _healthBehaviour;
    private MaterialPropertyBlock _propertyBlock;
    private Color _originalColor;
    private Coroutine _flashCoroutine;

    private static readonly int ColorId = Shader.PropertyToID("_BaseColor");

    private void Awake()
    {
        _renderer = GetComponent<Renderer>();
        _propertyBlock = new MaterialPropertyBlock();
    }

    private void Start()
    {
        _healthBehaviour = GetComponent<HealthBehaviour>();

        if (_healthBehaviour == null)
        {
            Debug.LogError("EnemyHitFlash on " + name + " requires a HealthBehaviour component", this);
            enabled = false;
            return;
        }

        if (_renderer == null)
        {
            Debug.LogError("EnemyHitFlash on " + name + " requires a Renderer component", this);
            enabled = false;
            return;
        }

        // read original color from the shared material, not the property block
        _originalColor = _renderer.sharedMaterial.HasProperty(ColorId)
            ? _renderer.sharedMaterial.GetColor(ColorId)
            : Color.white;

        _healthBehaviour.Health.OnDamaged += OnDamaged;
        _healthBehaviour.Health.OnDeath += OnDeath;
    }

    private void OnDisable()
    {
        if (_healthBehaviour != null)
        {
            _healthBehaviour.Health.OnDamaged -= OnDamaged;
            _healthBehaviour.Health.OnDeath -= OnDeath;
        }
    }

    private void OnDamaged(DamageData damage)
    {
        Flash(Color.white, _flashDuration, _originalColor);
    }

    private void OnDeath()
    {
        // flash red briefly before FaunaController destroys the object (~2s window)
        Flash(Color.red, 0.5f, _originalColor);
    }

    private void Flash(Color flashColor, float duration, Color returnColor)
    {
        if (_flashCoroutine != null)
            StopCoroutine(_flashCoroutine);

        _flashCoroutine = StartCoroutine(FlashRoutine(flashColor, duration, returnColor));
    }

    private IEnumerator FlashRoutine(Color flashColor, float duration, Color returnColor)
    {
        SetColor(flashColor);
        yield return new WaitForSeconds(duration);
        SetColor(returnColor);
        _flashCoroutine = null;
    }

    private void SetColor(Color color)
    {
        _renderer.GetPropertyBlock(_propertyBlock);
        _propertyBlock.SetColor(ColorId, color);
        _renderer.SetPropertyBlock(_propertyBlock);
    }
}
