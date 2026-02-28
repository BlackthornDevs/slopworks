public class WeaponController
{
    private readonly WeaponDefinitionSO _def;
    private float _fireCooldown;
    private float _reloadTimer;

    public int CurrentAmmo { get; private set; }
    public bool IsReloading { get; private set; }
    public bool CanFire => !IsReloading && CurrentAmmo > 0 && _fireCooldown <= 0f;
    public float Range => _def.range;

    public DamageData BuildDamageData(string sourceId)
    {
        return new DamageData(_def.damage, sourceId, _def.damageType);
    }

    public WeaponController(WeaponDefinitionSO definition)
    {
        _def = definition;
        CurrentAmmo = definition.magazineSize;
    }

    public bool TryFire()
    {
        if (!CanFire)
            return false;

        CurrentAmmo--;
        _fireCooldown = _def.fireRate > 0f ? 1f / _def.fireRate : 0f;
        return true;
    }

    public void Reload()
    {
        if (IsReloading)
            return;

        IsReloading = true;
        _reloadTimer = _def.reloadTime;
    }

    public void Tick(float deltaTime)
    {
        if (_fireCooldown > 0f)
            _fireCooldown -= deltaTime;

        if (IsReloading)
        {
            _reloadTimer -= deltaTime;
            if (_reloadTimer <= 0f)
            {
                CurrentAmmo = _def.magazineSize;
                IsReloading = false;
            }
        }
    }
}
