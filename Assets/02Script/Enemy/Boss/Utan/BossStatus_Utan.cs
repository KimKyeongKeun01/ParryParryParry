using UnityEngine;

[CreateAssetMenu(fileName ="UtanStatus", menuName ="Boss/Utan Status Data")]
public class BossStatus_Utan : BaseBossStatus
{
    [Tooltip("패턴 실행 전 최대 이동 시간")] public float maxApproachTime = 1.2f;
    [Tooltip("2페이즈 패턴 간격 배수"), Range(0.1f, 1f)] public float phase2PatternIntervalMulti = 0.85f;


    [Header(" === Utan ===")]
    [Tooltip("근거리 판정 거리")] public float meleeRange = 4f;
    [Tooltip("중거리 판정 거리")] public float midRange = 8f;

    [Header(" === Pattern: Arm Smash === ")]
    [Tooltip("양팔 휘두르기 쿨타임")] public float armSmashCooldown = 3f;
    [Tooltip("양팔 휘두르기 데미지")] public int armSmashDamage = 1;
    [Tooltip("양팔 휘두르기 넉백 파워")] public float armSmashKnockback = 10f;
    [Tooltip("양팔 휘두르기 퍼펙트 가드시 스턴 시간")] public float armSmashStunTime = 2f;
    [Tooltip("플레이어 위로의 텔레포트 추가 높이")] public float armSmashHeight = 6;
    [Tooltip("양팔 휘두르기 공격 범위")] public float armSmashMeleeRange = 5.5f;
    [Tooltip("양팔 휘두르기 공중 체공 시간")] public float armSmashAirDuration = 0.3f;
    [Tooltip("양팔 휘두르기 공중 체공 시간")] public float armSmashAirDuration_Phase2 = 0.6f;

    [Tooltip("플레이어에게 포물선으로 날아가는 시간")] public float dropDuration = 0.5f;
    [Tooltip("포물선의 높이")] public float arcHeight = 5f;

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

    [Header(" === [Phase2] Pattern: Double Swing === ")]
    [Tooltip("더블스윙 쿨타임")] public float doubleSwingCooldown;
    
    [Header(" === [Phase2] Pattern: Volleyball Combo === ")]
    [Tooltip("콤보 쿨타임")] public float comboCooldown;
    [Tooltip("백스텝 이동 거리")] public float comboBackstepDist;
    [Tooltip("백스텝 소요 시간")] public float comboBackstepTime;
    [Tooltip("도약 높이")] public float comboTossHeight;
    [Tooltip("공을 칠 때 속도 배율")] public int comboSpikeSpeed;

    [Header(" === Slam Window === ")]
    [Tooltip("기절시 슬램 가능한 시간")] public float slamWindownDuration = 3f;
    [Tooltip("첫 번째 스윙의 올라갈 y offset")]public float doubleSwingFirstHeightOffset;
}
