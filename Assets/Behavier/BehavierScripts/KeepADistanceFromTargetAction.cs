using System;
using Unity.Behavior;
using UnityEngine;
using Action = Unity.Behavior.Action;
using Unity.Properties;

[Serializable, GeneratePropertyBag]
[NodeDescription(name: "Keep a distance from Target", story: "Agent keep a [Distance] from [Target]", category: "Action/Transform", id: "04c4e930ed9bbb3c0fee471d45b8a6cd")]
public partial class KeepADistanceFromTargetAction : Action
{
    [SerializeReference] public BlackboardVariable<Vector2> Distance;
    [SerializeReference] public BlackboardVariable<GameObject> Target;

    protected override Status OnStart()
    {
        return Status.Running;
    }

    protected override Status OnUpdate()
    {
        return Status.Success;
    }

    protected override void OnEnd()
    {

    }
}

