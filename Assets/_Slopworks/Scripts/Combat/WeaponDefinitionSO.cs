using UnityEngine;

[CreateAssetMenu(menuName = "Slopworks/Combat/Weapon Definition")]
public class WeaponDefinitionSO : ScriptableObject
{
    public string weaponId;
    public float damage = 10f;
    public float fireRate = 5f;
    public float range = 50f;
    public DamageType damageType = DamageType.Kinetic;
    public int magazineSize = 30;
    public float reloadTime = 2f;
}
