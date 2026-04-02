using System;
using Unity.Behavior;
using UnityEngine;
using Action = Unity.Behavior.Action;
using Unity.Properties;

[Serializable, GeneratePropertyBag]
[NodeDescription(name: "Check Distance To Player", story: "[Self] distance to [Player] is less than [Range]", category: "Enemy/Secnsing", id: "7498c88324189ffe6ce40930228bc844")]
public partial class CheckDistanceToPlayerAction : Action
{
    [SerializeReference] public BlackboardVariable<GameObject> Self;
    [SerializeReference] public BlackboardVariable<GameObject> Player;
    [SerializeReference] public BlackboardVariable<float> Range;

    protected override Status OnUpdate()
    {
        if (Self.Value == null || Player.Value == null)
            return Status.Failure;

        float dist = Mathf.Abs(
            Self.Value.transform.position.x - Player.Value.transform.position.x);

        return dist < Range.Value ? Status.Success : Status.Failure;
    }
}