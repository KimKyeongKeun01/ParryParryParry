using System.Collections;
using UnityEngine;

/// <summary>
/// TimeScale 요청만 받아서 우선순위에 따라 최종 Time.timeScale을 적용하는 최소 관리자.
/// 
/// 우선순위 규칙:
/// - SystemTime : 상위 우선순위 (Pause, 메뉴, 컷신 정지 등)
/// - ActionTime : 하위 우선순위 (슬램, 퍼펙트가드, 히트스톱 등)
/// - 최종값은 Mathf.Min(systemScale, actionScale)
/// 
/// Ex)
/// - System = 0, Action = 0.6  -> 최종 0
/// - System = 1, Action = 0.6  -> 최종 0.6
/// - System = 1, Action = 1    -> 최종 1
/// </summary>
public class TimeManager : MonoBehaviour
{
    public enum TimeRequestGroup
    {
        System,
        Action
    }


    [Header("Base Settings")]
    [Tooltip("기본 Fixed Delta Time 값. 보통 0.02")]
    [SerializeField] private float baseFixedDeltaTime = 0.02f;
    [Tooltip("시작 시 현재 Time.fixedDeltaTime 값을 baseFixedDeltaTime으로 덮어쓸지 여부")]
    [SerializeField] private bool useCurrentFixedDeltaTimeAsBase = true;

    private float systemScale = 1f;
    private float actionScale = 1f;

    private Coroutine actionRoutine;


    #region 프로퍼티
    public float CurrentTimeScale => Mathf.Min(systemScale, actionScale);
    public bool IsPaused => Mathf.Approximately(systemScale, 0f);
    #endregion


    private void Awake()
    {
        if (useCurrentFixedDeltaTimeAsBase) baseFixedDeltaTime = Time.fixedDeltaTime;

        Apply();
    }

    #region Core
    public void SetTimeScale(float scale, float duration = 0f, bool isSystem = false)
    {
        if (isSystem)
            SetSystemTime(scale);
        else
            SetActionTime(scale, duration);
    }

    #region System
    public void SetSystemTime(float scale)
    {
        systemScale = Mathf.Clamp01(scale);
        Apply();
    }

    /// <summary>
    /// 시스템용 시간값 설정.
    /// </summary>
    public void ResetSystemTime()
    {
        systemScale = 1f;
        Apply();
    }
    #endregion

    #region Action
    /// <summary>
    /// 액션용 시간값 설정.
    /// </summary>
    public void SetActionTime(float scale, float duration)
    {
        // Scale 값은 0(정지)과 1(재생) 사이로 제한
        actionScale = Mathf.Clamp01(scale);
        // 배속 구현시 해당 주석 사용
        //actionScale = Mathf.Clamp(scale, 0f, 최대배속);
        Apply();

        if (actionRoutine != null)
        {
            StopCoroutine(actionRoutine);
            actionRoutine = null;
        }

        if (duration > 0f)
            actionRoutine = StartCoroutine(Co_ResetActionTime(duration));
    }

    /// <summary>
    /// 액션용 시간 복구.
    /// </summary>
    public void ResetActionTime()
    {
        if (actionRoutine != null)
        {
            StopCoroutine(actionRoutine);
            actionRoutine = null;
        }

        actionScale = 1f;
        Apply();
    }
    #endregion

    /// <summary>
    /// 전체 시간값 복구.
    /// </summary>
    public void ResetAllTime()
    {
        ResetActionTime();
        ResetSystemTime();
    }
    #endregion

    #region Internal
    /// <summary>
    /// 최종 Time.timeScale 적용.
    /// </summary>
    private void Apply()
    {
        /* 시간 적용 법칙
        Scale이 더 작은 시간 적용
        - System = 0, Action = 0.6  -> 최종 0
        - System = 1, Action = 0.6  -> 최종 0.6
        - System = 1, Action = 1    -> 최종 1
        */
        float finalScale = Mathf.Min(systemScale, actionScale);

        Time.timeScale = finalScale;
        Time.fixedDeltaTime = baseFixedDeltaTime * finalScale;
    }

    /// <summary>
    /// ActionTime은 실시간 기준 duration 후 자동 복구.
    /// timeScale이 0이어도 정상 종료되어야 하므로 WaitForSecondsRealtime 사용.
    /// </summary>
    private IEnumerator Co_ResetActionTime(float duration)
    {
        float elapsed = 0f;

        while (elapsed < duration)
        {
            // 시스템 정지시 액션 타이머 정지
            if (!IsPaused)
                elapsed += Time.unscaledDeltaTime;

            yield return null;
        }

        actionRoutine = null;
        actionScale = 1f;
        Apply();
    }
    #endregion

    private void OnDestroy()
    {
        // 종료 시 안전하게 원복
        Time.timeScale = 1f;
        Time.fixedDeltaTime = baseFixedDeltaTime;
    }
}
