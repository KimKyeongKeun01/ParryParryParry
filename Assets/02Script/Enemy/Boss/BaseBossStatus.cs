using UnityEngine;

[CreateAssetMenu(fileName = "NewBossStatus", menuName = "Boss/Status Data")]
public class BaseBossStatus : ScriptableObject
{
    [Header(" === Basic Info === ")]
    [Tooltip("보스 이름")] public string bossName;
    [Tooltip("최대 체력")] public int maxHp = 100;
    [Tooltip("플레이어 피격시 데미지(몸체 피격)")] public int contactDamage = 1;

    [Header(" === Move/Detect === ")]
    [Tooltip("이동 속도")] public float moveSpeed = 3f;
    [Tooltip("플레이어 탐지 거리")] public float detectRange = 10f;
    [Tooltip("플레이어 탐지 높이")] public float detectHeight = 5f;

    [Header(" === Phase Management === ")]
    [Tooltip("페이즈 전환 체력 비율")][Range(0.1f, 0.9f)] public float phase2Threshold = 0.5f;
    [Tooltip("페이즈 전환 시간")] public float phaseTransitionDelay = 2f;

    [Header(" === Groggy (Exhausted/Stun) === ")]
    [Tooltip("탈진 지속 시간")] public float exhaustedDuration = 5f;
    [Tooltip("탈진시 피격 제한 횟수")] public int exhaustedHitLimit = 5;
    [Tooltip("탈진시 데미지 배율")] public float exhaustedDamageMulti = 1f;

    [Header(" === Pattern Setting === ")]
    [Tooltip("공격 전 딜레이 시간")] public float defultWindUpTime = 0.5f;
    [Tooltip("공격 후 딜레이 시간")] public float defultWindDownTime = 0.5f;
    [Tooltip("패턴 사이 대기 시간")] public float patternInterval = 1.5f;
}
