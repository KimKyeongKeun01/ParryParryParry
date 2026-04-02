using System;
using Unity.Behavior;
using UnityEngine;
using Action = Unity.Behavior.Action;
using Unity.Properties;

[Serializable, GeneratePropertyBag]
[NodeDescription(name: "UpdateDistance", story: "Update [Self] and [Target] [CurrentDistance]", category: "Action", id: "b7e272e395ec32768e1fa2a51c911f41")]
public partial class UpdateDistanceAction : Action
{
    [SerializeReference] public BlackboardVariable<GameObject> Self;
    [SerializeReference] public BlackboardVariable<GameObject> Target;
    [SerializeReference] public BlackboardVariable<float> CurrentDistance;


    protected override Status OnUpdate()
    {
        CurrentDistance.Value = Vector2.Distance(Self.Value.transform.position, Target.Value.transform.position);

        return Status.Success;
    }

    
}

