using System;
using Unity.Behavior;

[BlackboardEnum]
public enum EnemyState
{
	Idle,
	Patrol,
	Chase,
	Flee,
	Attack,
    SpecialBehavior
}
