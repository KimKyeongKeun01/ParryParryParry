using System;
using Unity.Behavior;
using UnityEngine;
using Action = Unity.Behavior.Action;
using Unity.Properties;

[Serializable, GeneratePropertyBag]
[NodeDescription(name: "Flee From Player", story: "[Self] flees from [Player] at [MoveSpeed] until [FleeRange] and sets [IsTrapped]", category: "Enemy/Movement", id: "8bd4ffb64c53e0e50acb4bf8dbfff8d2")]
public partial class FleeFromPlayerAction : Action
{
    [SerializeReference] public BlackboardVariable<GameObject> Self;
    [SerializeReference] public BlackboardVariable<GameObject> Player;
    [SerializeReference] public BlackboardVariable<float> MoveSpeed;
    [SerializeReference] public BlackboardVariable<float> FleeRange;
    [SerializeReference] public BlackboardVariable<bool> IsTrapped;

    private Rigidbody2D rb;
    private SpriteRenderer sr;

    protected override Status OnStart()
    {
        rb = Self.Value.GetComponent<Rigidbody2D>();
        sr = Self.Value.GetComponentInChildren<SpriteRenderer>();
        IsTrapped.Value = false;
        return Status.Running;
    }

    protected override Status OnUpdate()
    {
        if (Player.Value == null || Self.Value == null)
            return Status.Failure;

        float selfX = Self.Value.transform.position.x;
        float playerX = Player.Value.transform.position.x;
        float distX = Mathf.Abs(selfX - playerX);

        // 충분히 멀어졌으면 도주 성공
        if (distX >= FleeRange.Value)
        {
            StopMovement();
            return Status.Success;
        }

        // 도망 방향 = 플레이어 반대
        float dir = playerX > selfX ? -1f : 1f;

        // 절벽 또는 벽에 막혔는지 체크
        if (IsEdgeAhead(dir) || IsWallAhead(dir))
        {
            StopMovement();
            IsTrapped.Value = true;  // Blackboard에 갇힘 상태 기록
            return Status.Failure;   // 상위 노드로 제어 반환
        }

        rb.linearVelocity = new Vector2(dir * MoveSpeed.Value, rb.linearVelocity.y);
        sr.flipX = dir < 0;

        return Status.Running;
    }

    protected override void OnEnd()
    {
        StopMovement();
    }

    private void StopMovement()
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
        return hit.collider == null;
    }

    private bool IsWallAhead(float dir)
    {
        int groundLayer = LayerMask.GetMask("Ground");
        Vector2 origin = (Vector2)Self.Value.transform.position;
        RaycastHit2D hit = Physics2D.Raycast(origin, new Vector2(dir, 0), 0.6f, groundLayer);
        return hit.collider != null;
    }
}

