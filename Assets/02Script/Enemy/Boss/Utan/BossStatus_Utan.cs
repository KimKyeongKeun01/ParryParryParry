using UnityEngine;

[CreateAssetMenu(fileName ="UtanStatus", menuName ="Boss/Utan Status Data")]
public class BossStatus_Utan : BaseBossStatus
{
    [Tooltip("패턴 실행 전 최대 이동 시간")] public float maxApproachTime = 1.2f;
    [Tooltip("2페이즈 패턴 간격 배수"), Range(0.1f, 1f)] public float phase2PatternIntervalMulti = 0.85f;


    [Header(" === Utan ===")]
    [Tooltip("근거리 판정 거리")] public float meleeRange = 4f;
    [Tooltip("중거리 판정 거리")] public float midRange = 8f;

    [Header(" === Pattern: Arm Smash")]
    [Tooltip("양팔 휘두르기 최소 거리")] public float armSmashStopDistance = 2.8f;
    [Tooltip("양팔 휘두르기 쿨타임")] public float armSmashCooldown = 3f;
    [Tooltip("양팔 휘두르기 데미지")] public int armSmashDamage = 1;
    [Tooltip("양팔 휘두르기 넉백 파워")] public float armSmashKnockback = 10f;
    [Tooltip("양팔 휘두르기 퍼펙트 가드시 스턴 시간")] public float armSmashStunTime = 2f;

    [Header(" === Pattern: Rock Throw === ")]
    [Tooltip("암석 뽑기 쿨타임")] public float rockThrowCooldown = 5f;
    [Tooltip("암석 뽑기 데미지")] public int rockDamage = 1;
    [Tooltip("암석 넉백 파워")] public float rockKnockback = 10f;
    [Tooltip("암석 이동 속도")] public float rockSpeed = 12f;
    [Tooltip("암석 포물선 최고 높이")] public float rockApexOffset = 5f;

    [Header(" === Pattern: Swing === ")]
    [Tooltip("나무 타기 높이")] public float swingHeight = 5f;
    [Tooltip("나무 타기 줄 길이")] public float swingLopeLength = 10f;
    [Tooltip("나무 타기 쿨타임")] public float swingCooldown = 10f;
    [Tooltip("나무 타기 데미지")] public int swingDamage = 1;
    [Tooltip("나무 타기 넉백 파워")] public float swingKnockback = 10f;
    [Tooltip("나무 타기 이동 속도")] public float swingSpeed = 15f;
    [Tooltip("나무 타기 시간")] public float swingDuration = 3f;

    [Header(" === Slam Window === ")]
    [Tooltip("기절시 슬램 가능한 시간")] public float slamWindownDuration = 3f;
}
