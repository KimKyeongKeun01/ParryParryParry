using System.Collections;
using UnityEngine;

public abstract class BaseBoss : BaseEnemy
{
    [Header(" === Base Boss === ")]
    [Header("Boss Components")]
    public BaseBossStatus _status;
    public BaseBossVisual _visual;

    [Header("Boss State")]
    [Tooltip("현재 슬램 가능 여부")] protected bool canSlam = false;
    [Tooltip("현재 페이즈")][SerializeField] protected int phase = 1;
    [Tooltip("2페이즈 돌입구간")][SerializeField] protected float phase2Threshold = 0.4f;
    [Tooltip("보스 페이즈 변경 중 플래그")] protected bool isPhaseTransitioning = false;

    [Header("Boss Stage")]
    [Tooltip("스테이지 왼쪽 끝 X 좌표")][SerializeField] public float stageLeftX;
    [Tooltip("스테이지 오른쪽 끝 X 좌표")][SerializeField] public float stageRightX;
    [Tooltip("벽 최소 간격 오프셋")][SerializeField] public float stageOffsetX;

    [Header("Boss Stun")]
    [Tooltip("보스 스턴 피격 제한 횟수")][SerializeField] protected int stunHitLimit = 3;
    [Tooltip("보스 현재 스턴 피격 횟수")] protected int curHits = 0;

    [Tooltip("현재 실행 중인 보스 패턴 코루틴")] protected Coroutine patternCoroutine;
    public bool isPlayingCutScene = false;
    protected override void Awake()
    {
        base.Awake();
        _visual = GetComponent<BaseBossVisual>();
        _visual?.Init(this);
        isPlayingCutScene=true;
        #region Status 동기화
        maxHp = _status.maxHp;
        curHp = maxHp;
        damage = _status.contactDamage;

        detectRange = _status.detectRange;
        detectHeight = _status.detectHeight;

        phase2Threshold = _status.phase2Threshold;

        stunDuration = _status.exhaustedDuration;
        stunHitLimit = _status.exhaustedHitLimit;
        #endregion
    }

    #region 상태 전환
    public override void ChangeState(EnemyState state)
    {
        // 기절 상태 초기화
        if (CurState == EnemyState.Stun) EndExhausted();

        // 공격 중이 아니면 기존에 실행 중인 루틴 중단
        if (state != EnemyState.Attack) StopPattern();

        // 피격 횟수 초기화
        if (state == EnemyState.Stun) curHits = 0;

        // 상태 전환
        base.ChangeState(state);
    }

    public void StopPattern()
    {
        if (patternCoroutine != null)
        {
            StopCoroutine(patternCoroutine);
            patternCoroutine = null;
        }
    }

    public override bool CanSlam()
    {
        return canSlam;
    }
    #endregion

    #region 페이즈 관리
    /// <summary> 체력 비례 페이즈 체크 메소드 </summary>
    protected virtual void CheckPhase()
    {
        if (isPhaseTransitioning) return;
        if (phase != 1) return;
        if (maxHp <= 0) return;

        if ((float)curHp / maxHp <= phase2Threshold)
        {
            StartCoroutine(Co_PhaseTransition());
        }
    }

    /// <summary> 페이즈 전환 중 코루틴 </summary>
    protected abstract IEnumerator Co_PhaseTransition();
    #endregion

    #region 공격 패턴
    public override void OnAttack()
    {
        if (patternCoroutine != null) return;

        patternCoroutine = StartCoroutine(Co_PatternCycle());
    }

    /// <summary> 공격 패턴 사이클 코루틴 </summary>
    protected virtual IEnumerator Co_PatternCycle()
    {
        while (!isDead && CurState == EnemyState.Attack) 
        {
            // 1. 패턴 선택
            int patternIndex = SelectNextPattern();


            if (patternIndex < 0)
            {
                yield return null;
                continue;
            }

            // 2. 패턴 실행
            yield return StartCoroutine(Co_StartPattern(patternIndex));

            if (isDead || CurState != EnemyState.Attack) break;

            // 3. 패턴 간 대기 시간
            yield return new WaitForSeconds(GetPatternInterval());
        }

        patternCoroutine = null;
    }

    /// <summary> 패턴 선택 메소드 </summary>
    protected abstract int SelectNextPattern();

    /// <summary> 패턴 사이 간격 획득 메소드 </summary>
    protected abstract float GetPatternInterval();

    /// <summary> 공격 패턴 실행 코루틴 </summary>
    protected abstract IEnumerator Co_StartPattern(int index);
    #endregion

    #region 기절
    protected virtual void StartExhausted()
    {
        if (patternCoroutine != null) StopCoroutine(patternCoroutine);
        ChangeState(EnemyState.Stun);

        _coll.isTrigger = true;
        canSlam = true;
    }

    protected virtual void EndExhausted()
    {
        _coll.isTrigger = false;

        canSlam = false;
        stateTimer = 0; // Stun 강제 종료
    }
    #endregion

    #region 피격
    public override void TakeDamage(int damage)
    {
        // 페이즈 전환 중 무적
        if (isPhaseTransitioning) return;

        base.TakeDamage(damage);
    }
    #endregion

    #region 환경
    public void InitStage(float left, float right, float offset)
    {
        stageLeftX = left;
        stageRightX = right;
        stageOffsetX = offset;
    }

    protected virtual void ValidateStage()
    {
        Vector3 localPos = transform.localPosition;

        localPos.x = Mathf.Clamp(localPos.x, stageLeftX, stageRightX);
        transform.localPosition = localPos;

        if (_rigid != null)
        {
            // 로컬 좌표 수정 후 월드 좌표로 변환하여 Rigidbody 위치를 강제 동기화
            _rigid.position = transform.position;
        }
    }
    #endregion
}
