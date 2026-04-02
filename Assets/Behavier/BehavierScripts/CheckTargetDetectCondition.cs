using System;
using Unity.Behavior;
using UnityEngine;

[Serializable, Unity.Properties.GeneratePropertyBag]
[Condition(name: "CheckTargetDetect", story: "Compare Values of [CurrentDistance] and [ChaseDistance]", category: "Conditions", id: "f164a654b0e4059889b9bb629791e539")]
public partial class CheckTargetDetectCondition : Condition
{
    [SerializeReference] public BlackboardVariable<float> CurrentDistance;
    [SerializeReference] public BlackboardVariable<float> ChaseDistance;

    public override bool IsTrue()
    {
        if (CurrentDistance.Value <= ChaseDistance.Value)
        {
            return true;
        }

        return false;
    }
}
