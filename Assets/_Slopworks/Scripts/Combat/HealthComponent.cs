using System;

public class HealthComponent
{
    public float CurrentHealth { get; private set; }
    public float MaxHealth { get; }
    public bool IsAlive => CurrentHealth > 0f;

    public event Action<DamageData> OnDamaged;
    public event Action OnDeath;

    private bool _deathFired;

    public HealthComponent(float maxHealth)
    {
        MaxHealth = maxHealth;
        CurrentHealth = maxHealth;
    }

    public void TakeDamage(DamageData damage)
    {
        if (!IsAlive)
            return;

        CurrentHealth = Math.Max(0f, CurrentHealth - damage.amount);
        OnDamaged?.Invoke(damage);

        if (!IsAlive && !_deathFired)
        {
            _deathFired = true;
            OnDeath?.Invoke();
        }
    }

    public void Heal(float amount)
    {
        if (!IsAlive)
            return;

        CurrentHealth = Math.Min(MaxHealth, CurrentHealth + amount);
    }
}
