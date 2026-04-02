using UnityEngine;

public class EnemyStats : MonoBehaviour
{
    [Header("Stats")]
    public float maxHp = 100f;
    public float currentHp;
    public float attackDamage = 10f;
    public float moveSpeed = 3f;

    [Header("AI Ranges")]
    public float detectionRange = 10f;
    public float attackRange = 6f;
    public float fleeRange = 2.5f;

    public bool IsAlive => currentHp > 0f;

    void Awake() => currentHp = maxHp;
}