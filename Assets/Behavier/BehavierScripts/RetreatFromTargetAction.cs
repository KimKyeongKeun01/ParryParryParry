using System;
using Unity.Behavior;
using UnityEngine;
using Action = Unity.Behavior.Action;
using Unity.Properties;

[Serializable, GeneratePropertyBag]
[NodeDescription(name: "Retreat From Target", story: "[Self] runs away from [Target] while keeping DesiredDistance", category: "Action", id: "0570d838e99ced107d76a87f2e2919bc")]
public partial class RetreatFromTargetAction : Action
{
    [SerializeReference] public GameObject Self;
    [SerializeReference] public GameObject Target;

    [SerializeField] private SpriteRenderer _sprite;
    protected float facingX = 1f;
    public float DesiredDistance = 5f;
    public float MoveSpeed = 5f;
    protected override Status OnStart()
    {
        if (Self == null || Target == null)
            return Status.Failure;

        return Status.Running;
    }

    protected override Status OnUpdate()
    {
        if (Self == null || Target == null)
            return Status.Failure;

        Vector3 selfPos = Self.transform.position;
        Vector3 targetPos = Target.transform.position;

        float dx = selfPos.x - targetPos.x;
        float distanceX = Mathf.Abs(dx);

        // 이미 충분히 떨어져 있으면 정지
        if (distanceX >= DesiredDistance)
            return Status.Running;

        // 플레이어 반대 방향으로만 x축 이동
        float dir = dx >= 0 ? 1f : -1f;

        selfPos.x += dir * MoveSpeed * Time.deltaTime;
        Self.transform.position = selfPos;

        UpdateFlip();

        return Status.Success;
    }

    protected override void OnEnd()
    {
    }

    private void UpdateFlip()
    {
        if (_sprite != null) _sprite.flipX = facingX < 0;
    }
}

