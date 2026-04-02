using System;
using Unity.Behavior;
using UnityEngine;
using Action = Unity.Behavior.Action;
using Unity.Properties;

[Serializable, GeneratePropertyBag]
[NodeDescription(name: "Check Player In Sight", story: "[Self] detects [Player] within [DetectionRange] with line of sight", category: "Enemy/Secnsing", id: "af5635308e5577078b2c4e65f4d6c5e2")]
public partial class CheckPlayerInSightAction : Action
{
    [SerializeReference] public BlackboardVariable<GameObject> Self;
    [SerializeReference] public BlackboardVariable<GameObject> Player;
    [SerializeReference] public BlackboardVariable<float> DetectionRange;

    protected override Status OnUpdate()
    {
        if (Self.Value == null || Player.Value == null)
            return Status.Failure;

        Vector2 selfPos = Self.Value.transform.position;
        Vector2 playerPos = Player.Value.transform.position;
        float dist = Vector2.Distance(selfPos, playerPos);

        if (dist > DetectionRange.Value)
            return Status.Failure;

        // X축 방향만 감지 (2D 사이드뷰)
        Vector2 dir = (playerPos - selfPos).normalized;

        // Raycast로 벽 감지 (Ground 레이어만 충돌)
        int groundLayer = LayerMask.GetMask("Ground");
        RaycastHit2D hit = Physics2D.Raycast(selfPos, dir, dist, groundLayer);

        // 벽에 막히지 않으면 Success
        return hit.collider == null ? Status.Success : Status.Failure;
    }
}

