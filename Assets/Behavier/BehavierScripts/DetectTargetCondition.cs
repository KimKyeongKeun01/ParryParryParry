using System;
using Unity.Behavior;
using UnityEngine;

[Serializable, Unity.Properties.GeneratePropertyBag]
[Condition(name: "Detect Target", story: "[Self] detects [Target] within [DetectRange] x [DetectHeight] with offset [DetectStartOffsetX] [DetectStartOffsetY], Layer is [GroundLayer]", category: "Conditions", id: "1d83d20e71b593a7807835666476178b")]
public partial class DetectTargetCondition : Condition
{
    [SerializeReference] public BlackboardVariable<GameObject> Self;
    [SerializeReference] public BlackboardVariable<GameObject> Target;
    [SerializeReference] public BlackboardVariable<float> DetectRange;
    [SerializeReference] public BlackboardVariable<float> DetectHeight;
    [SerializeReference] public BlackboardVariable<float> DetectStartOffsetX;
    [SerializeReference] public BlackboardVariable<float> DetectStartOffsetY;

    [SerializeReference] public BlackboardVariable<string> GroundLayer;
    private LayerMask groundMask;
    private void Awake()
    {
        
    }

    public override bool IsTrue()
    {

        if (Self.Value == null || Target.Value == null) return false;

        Collider2D agentColl = Self.Value.GetComponent<Collider2D>();
        Collider2D playerColl = Target.Value.GetComponent<Collider2D>();
        if (agentColl == null || playerColl == null) return false;

        // facingX: 에이전트가 바라보는 방향 (로컬 스케일 X의 부호로 판단)
        float facingX = Mathf.Sign(Self.Value.transform.localScale.x);

        // 탐지 시작점
        Bounds agentBounds = agentColl.bounds;
        Vector2 origin = new Vector2(
            agentBounds.center.x + facingX * (agentBounds.extents.x + DetectStartOffsetX.Value),
            agentBounds.center.y + DetectStartOffsetY.Value
        );

        // 플레이어 중심
        Vector2 target = playerColl.bounds.center;
        Vector2 toTarget = target - origin;

        // 1. 방향 체크
        if (toTarget.x * facingX <= 0f) return false;

        // 2. 범위 체크
        if (Mathf.Abs(toTarget.x) > DetectRange.Value) return false;
        if (Mathf.Abs(toTarget.y) > DetectHeight.Value) return false;

        groundMask = LayerMask.GetMask(GroundLayer.Value);
        // 3. 시야 장애물 체크
        RaycastHit2D hit = Physics2D.Linecast(origin, target, groundMask);
        if (hit.collider != null) return false;

        return true;
    }
}
