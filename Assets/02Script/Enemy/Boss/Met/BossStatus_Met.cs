using UnityEngine;

[CreateAssetMenu(fileName = "MetStatus", menuName = "Boss/Met Status Data")]
public class BossStatus_Met : BaseBossStatus
{
    [Tooltip("패턴 실행 전 최대 접근 시간")] public float maxApproachTime = 1.0f;

    [Header(" === Met === ")]
    [Tooltip("근접 판정 거리")] public float meleeRange = 3.5f;

    [Header(" === Pattern: Dash (돌진) === ")]
    [Tooltip("돌진 속도")] public float dashSpeed = 18f;
    [Tooltip("돌진 쿨타임")] public float dashCooldown = 6f;
    [Tooltip("돌진 데미지")] public int dashDamage = 1;
    [Tooltip("돌진 넉백 파워")] public float dashKnockback = 12f;
    [Tooltip("돌진 벽감지 거리")] public float wallDetectRange = 1.0f;
    [Tooltip("벽 충돌 시 카메라 흔들림 파워")] public float wallHitShake = 0.5f;

    [Header(" === Pattern: Tusk Sweep (엄니 쓸어올리기) === ")]
    [Tooltip("엄니 공격 사거리")] public float tuskRange = 3f;
    [Tooltip("엄니 공격 속도")] public float tuskSpeed = 3f;
    [Tooltip("엄니 공격 쿨타임")] public float tuskCooldown = 3f;
    [Tooltip("엄니 공격 데미지")] public int tuskDamage = 1;
    [Tooltip("엄니 수직 넉백 파워")] public float tuskUpKnockback = 15f;
    

    [Header(" === Pattern: Body Slam (몸통 들이받기) === ")]
    [Tooltip("들이받기 사거리")] public float slamRange = 4f;
    [Tooltip("들이받기 속도")] public float slamSpeed = 4f;
    [Tooltip("들이받기 쿨타임")] public float slamCooldown = 4f;
    [Tooltip("들이받기 데미지")] public int slamDamage = 1;
    [Tooltip("들이받기 수평 넉백 파워")] public float slamXForce = 14f;
    [Tooltip("들이받기 가드 시 넉백 파워")] public float slamGuardKnockback = 4f;


    [Header("Ground Slam")]
    public float groundSlamJumpHeight = 8f;    // 올라가는 높이
    public float groundSlamRiseSpeed = 3f;    // 올라가는 속도
    public float groundSlamDownSpeed = 5f;    // 내려찍는 속도
    public float groundSlamCooldown = 20f;    // 패턴 쿨타임
    public int groundSlamDamage = 1;     // 데미지
    public float groundSlamKnockback = 15f;   // 넉백 강도
}
