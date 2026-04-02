using System;
using Unity.Behavior;
using UnityEngine;
using Action = Unity.Behavior.Action;
using Unity.Properties;

[Serializable, GeneratePropertyBag]
[NodeDescription(name: "Range Detector", story: "Update Range [Detector] and Assign [Target]", category: "Action", id: "8a73c3541525b657a516a137ca929477")]
public partial class RangeDetectorAction : Action
{
    [SerializeReference] public BlackboardVariable<RangeDetector> Detector;
    [SerializeReference] public BlackboardVariable<GameObject> Target;


    protected override Status OnUpdate()
    {
        Target.Value = Detector.Value.UpdateDetector();
        return Detector.Value !=null? Status.Failure : Status.Success;
    }

}

