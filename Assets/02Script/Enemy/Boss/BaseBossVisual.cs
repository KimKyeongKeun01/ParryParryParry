using System.Collections;
using UnityEngine;

public abstract class BaseBossVisual : MonoBehaviour
{
    [Header(" === Visual === ")]
    [SerializeField] protected SpriteRenderer[] _sprites;
    [SerializeField] protected Animator _anim;
    public bool IsAnimFinished { get; protected set; } = true;

    [Header(" === Damage/Death ===")]
    [Tooltip("1페이즈 기본 새강"), SerializeField] protected Color originColor;
    [Tooltip("피격시 색상"), SerializeField] protected Color hitColor = Color.white;
    [Tooltip("피격 깜빡임 간격"), SerializeField] protected float blinkInterval = 0.08f;
    [Tooltip("피격 깜빡임 횟수"), SerializeField] protected int blinkCount = 3;
    [Tooltip("사망시 페이드 아웃 시간"), SerializeField] protected float fadeOutDuration = 2f;

    [Header(" === Exhausted/Stun === ")]
    [Tooltip("그로기 색상"), SerializeField] protected Color groggyColor = Color.yellow;

    // Internal Variables
    protected BaseBoss boss;
    protected Coroutine blinkRoutine;
    protected float originalScaleX;

    #region 초기화
    public virtual void Init(BaseBoss _boss)
    {
        boss = _boss;
        if (_anim == null) _anim = GetComponentInChildren<Animator>();
        _sprites = GetComponentsInChildren<SpriteRenderer>();
        
        foreach (var s in _sprites)
        {
            s.color = originColor;
            originalScaleX = gameObject.transform.localScale.x;
        }

        Debug.Log($"[Boss Visual] {gameObject.name} Init Complete");
    }
    #endregion

    #region 방향 전환
    public virtual void Flip(bool facingRight)
    {
        Vector3 scale = transform.localScale;

        scale.x = facingRight ? -originalScaleX : originalScaleX;
        transform.localScale = scale;
    }
    #endregion

    #region 상태별 비주얼
    /// <summary> 시각 효과 초기화 </summary>
    public virtual void InitVisual()
    {
        StopAllCoroutines();

        foreach (var s in _sprites)
        {
            s.enabled = true;
            s.color = originColor;
            //_sprite.transform.localRotation = Quaternion.identity;
        }
    }

    /// <summary> 그로기 상태 컬러 세팅(필요시 구현) </summary>
    public virtual void OnStunVisual()
    {
        Debug.Log("[Boss Visual] Groggy Enable");
        foreach (var s in _sprites) s.color = groggyColor;
    }

    /// <summary> 그로기 해제 컬러 세팅(필요시 구현) </summary>
    public virtual void OffStunVisual()
    {
        Debug.Log("[Boss Visual] Groggy Disable");
        foreach (var s in _sprites) s.color = originColor;
    }
    #endregion

    #region 애니메이션 실행
    /// <summary> 트리거형 애니메이션 재생 (공격, 피격 등) </summary>
    public void PlayAnim(string name)
    {
        _anim?.SetTrigger(name);

        IsAnimFinished = false;
    }

    /// <summary> 상태형 애니메이션 제어 (걷기, 기절 여부 등) </summary>
    public void PlayAnim(string name, bool value)
    {
        _anim?.SetBool(name, value);
    }

    /// <summary> 수치형 애니메이션 제어 (이동 속도 등) </summary>
    public virtual void PlayAnim(string name, float value)
    {
        _anim?.SetFloat(name, value);
    }

    /// <summary> 특정 스테이트로 즉시 전이 (강제 전환) </summary>
    public virtual void CrossFade(string name, float duration = 0.1f)
    {
        _anim?.CrossFade(name, duration);
    }

    public void ResetAnimTrigger(string triggerName)
    {
        // _anim은 Animator 컴포넌트 변수명에 맞춰 수정하세요.
        if (_anim != null) _anim.ResetTrigger(triggerName);
    }
    #endregion

    #region 애니메이션 이벤트
    public void AE_AnimFinished()
    {
        IsAnimFinished = true;
    }
    #endregion

    #region 페이즈 전환
    public virtual void PlayPhaseChange() { }

    /// <summary> 페이즈 전환 후 기본 컬러 세팅(필요시 구현) </summary>
    public virtual void OnPhaseChange(int phase, Color phaseColor)
    {
        originColor = phaseColor;

        foreach (var s in _sprites) s.color = originColor;
    }
    #endregion

    #region 피격 및 사망 비주얼
    public virtual void PlayHitEffect()
    {
        if (blinkRoutine != null) StopCoroutine(blinkRoutine);

        blinkRoutine = StartCoroutine(Co_Blink());
    }

    protected virtual IEnumerator Co_Blink()
    {
        for (int i = 0; i < blinkCount; i++)
        {
            // 알파값이 적용된 컬러로 변경
            foreach (var s in _sprites) s.color = new Color(_sprites[0].color.r, _sprites[0].color.g, _sprites[0].color.b, 0.2f);
            yield return new WaitForSeconds(blinkInterval);

            // 다시 원래 상태의 컬러(Alpha 1.0f 등)로 복구
            foreach (var s in _sprites) s.color = new Color(_sprites[0].color.r, _sprites[0].color.g, _sprites[0].color.b, 1f);
            yield return new WaitForSeconds(blinkInterval);
        }

        blinkRoutine = null;
    }

    public virtual void PlayDeathEffect()
    {
        StopAllCoroutines();
        StartCoroutine(Co_DeathFade());
    }

    protected virtual IEnumerator Co_DeathFade()
    {
        float elapsed = 0f;
        Color c = _sprites[0].color;

        while (elapsed < fadeOutDuration)
        {
            elapsed += Time.deltaTime;
            c.a = Mathf.Lerp(1f, 0f, elapsed / fadeOutDuration);
            foreach (var s in _sprites) s.color = c;
            yield return null;
        }
    }
    #endregion
}
