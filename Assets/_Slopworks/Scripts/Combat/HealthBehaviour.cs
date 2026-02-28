using UnityEngine;

public class HealthBehaviour : MonoBehaviour
{
    [SerializeField] private float _maxHealth = 100f;

    public HealthComponent Health { get; private set; }

    private void Awake()
    {
        Health = new HealthComponent(_maxHealth);
    }
}
