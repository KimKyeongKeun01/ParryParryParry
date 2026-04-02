using System;
using Unity.Behavior;
using UnityEngine;
using Action = Unity.Behavior.Action;
using Unity.Properties;

[Serializable, GeneratePropertyBag]
[NodeDescription(name: "Chase Player X Axis", story: "[Self] Chases [Player] at [MoveSpeed] stopping at [AttackRange]", category: "Enemy/Movement", id: "2e7a30705639dae948b84cb3d811aa68")]
public partial class ChasePlayerXAxisAction : Action
{
    [SerializeReference] public BlackboardVariable<GameObject> Self;
    [SerializeReference] public BlackboardVariable<GameObject> Player;
    [SerializeReference] public BlackboardVariable<float> MoveSpeed;
    [SerializeReference] public BlackboardVariable<float> AttackRange;

    private Rigidbody2D rb;

    protected override Status OnStart()
    {
        rb = Self.Value.GetComponent<Rigidbody2D>();
        return Status.Running;
    }

    protected override Status OnUpdate()
    {
        if (Player.Value == null || Self.Value == null)
            return Status.Failure;

        float selfX = Self.Value.transform.position.x;
        float playerX = Player.Value.transform.position.x;
        float distX = Mathf.Abs(selfX - playerX);

        // 공격 범위 내로 진입하면 종료
        if (distX <= AttackRange.Value)
            return Status.Success;

        float dir = playerX > selfX ? 1f : -1f;

        // 절벽 감지 (발 앞 아래쪽 Raycast)
        if (IsEdgeAhead(dir) || IsWallAhead(dir))
            return Status.Failure; // 막혔음 → 상위 노드가 처리

        // 이동
        rb.linearVelocity = new Vector2(dir * MoveSpeed.Value, rb.linearVelocity.y);


        return Status.Running;
    }

    protected override void OnEnd()
    {
        if (rb != null)
            rb.linearVelocity = new Vector2(0, rb.linearVelocity.y);
    }

    private bool IsEdgeAhead(float dir)
    {
        int groundLayer = LayerMask.GetMask("Ground");
        Vector2 origin = (Vector2)Self.Value.transform.position
                         + new Vector2(dir * 0.6f, -0.1f);
        RaycastHit2D hit = Physics2D.Raycast(origin, Vector2.down, 1.2f, groundLayer);
        return hit.collider == null; // 발 앞에 땅이 없으면 절벽
    }

    private bool IsWallAhead(float dir)
    {
        int groundLayer = LayerMask.GetMask("Ground");
        Vector2 origin = (Vector2)Self.Value.transform.position;
        RaycastHit2D hit = Physics2D.Raycast(origin, new Vector2(dir, 0), 0.6f, groundLayer);
        return hit.collider != null;
    }
}

