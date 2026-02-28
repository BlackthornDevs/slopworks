using UnityEngine;

[CreateAssetMenu(menuName = "Slopworks/Combat/Fauna Definition")]
public class FaunaDefinitionSO : ScriptableObject
{
    public string faunaId;
    public float maxHealth = 100f;
    public float moveSpeed = 3.5f;
    public float attackDamage = 10f;
    public float attackRange = 2f;
    public float attackCooldown = 1f;
    public float sightRange = 15f;
    public float sightAngle = 120f;
    public float hearingRange = 8f;
    public DamageType attackDamageType = DamageType.Kinetic;

    [Header("Pack behavior")]
    public float alertRange = 20f;
    public float strafeSpeed = 2.5f;
    public float strafeRadius = 3f;

    [Header("Morale")]
    public float baseBravery = 0.5f;
    public float fleeConfidenceThreshold = 0.3f;

    [Header("Cover")]
    public float coverSearchRadius = 10f;
}
