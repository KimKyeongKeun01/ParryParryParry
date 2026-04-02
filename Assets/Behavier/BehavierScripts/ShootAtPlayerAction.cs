using System;
using Unity.Behavior;
using Unity.Properties;
using UnityEngine;
using Action = Unity.Behavior.Action;

[Serializable, GeneratePropertyBag]
[NodeDescription(name: "Shoot At Player", story: "[Self] shoots projectile at [Player] with [Damage] damage", category: "Enemy/Combat", id: "0e6857e60b41f8fc9a12c918385138c0")]
public partial class ShootAtPlayerAction : Action
{
    [SerializeReference] public BlackboardVariable<GameObject> Self;
    [SerializeReference] public BlackboardVariable<GameObject> Player;
    [SerializeReference] public BlackboardVariable<float> Damage;

    public GameObject projectilePrefab;
    public float attackCooldown = 1.5f;

    private float lastAttackTime = -999f;

    protected override Status OnStart()
    {
        // EnemyStats에서 데미지 읽기
        if (Self.Value.TryGetComponent<EnemyStats>(out var stats))
            Damage.Value = stats.attackDamage;

        return Status.Running;
    }

    protected override Status OnUpdate()
    {
        if (Player.Value == null || Self.Value == null || projectilePrefab == null)
            return Status.Failure;

        // 쿨다운 대기
        if (Time.time - lastAttackTime < attackCooldown)
            return Status.Running;

        // 발사
        Vector2 selfPos = Self.Value.transform.position;
        Vector2 playerPos = Player.Value.transform.position;
        Vector2 dir = new Vector2(playerPos.x - selfPos.x, 0f).normalized;

        GameObject proj = UnityEngine.Object.Instantiate(projectilePrefab, selfPos, Quaternion.identity);
        if (proj.TryGetComponent<NewProjectile>(out var p))
            p.Init(dir, Damage.Value);

        lastAttackTime = Time.time;
        return Status.Running; // 계속 공격 상태 유지 (부모가 끊을 때까지)
    }
}
