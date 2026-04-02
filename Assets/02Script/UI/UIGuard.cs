using DG.Tweening;
using System.Collections;
using UnityEngine;
using UnityEngine.UI;

public class UIGuard : MonoBehaviour
{
    public enum GuardState { Normal, Active, Cooldown }
    private GuardState curState = GuardState.Normal;

    [Header(" UI Reference")]
    [SerializeField] private Slider slider;
    [SerializeField] private Image fImage;
    [SerializeField] private Image bImage;

    [Header("Visual")]
    [SerializeField] private Color normalColor = Color.white;
    [SerializeField] private Color activeColor = Color.mintCream;
    [SerializeField] private Color cooldownColor = Color.gray;

    
    private Tween pulseTween;
    private Coroutine cooldownRoutine;

    public void Init()
    {
        if (slider == null) slider = GetComponent<Slider>();

        slider.value = 1f;
        SetState(GuardState.Normal);
    }

    public void SetState(GuardState newState)
    {
        if (curState == newState) return;
        curState = newState;

        ResetAnim();

        switch (curState)
        {
            case GuardState.Normal:
                // 전체가 천천히 뽀용뽀용
                slider.value = 1f;
                fImage.DOColor(normalColor, 0.2f);
                pulseTween = transform.DOScale(1.05f, 0.8f).SetLoops(-1, LoopType.Yoyo).SetEase(Ease.InOutSine);

                break;
            case GuardState.Active:
                // 전체가 빠르게 뽀용뽀용
                if (cooldownRoutine != null) StopCoroutine(cooldownRoutine);
                slider.value = 1f;
                fImage.DOColor(activeColor, 0.1f);
                pulseTween = transform.DOScale(1.15f, 0.4f).SetLoops(-1, LoopType.Yoyo).SetEase(Ease.InOutSine);

                break;
            case GuardState.Cooldown:
                // 게이지(fImage)만 뽀용뽀용
                fImage.DOColor(cooldownColor, 0.2f);
                pulseTween = fImage.transform.DOScale(1.1f, 0.5f).SetLoops(-1, LoopType.Yoyo).SetEase(Ease.InOutSine);

                break;
        }
    }

    public void GuardCooldown(float duration)
    {
        if (cooldownRoutine != null) StopCoroutine(cooldownRoutine);
        cooldownRoutine = StartCoroutine(Co_GuardCool(duration));
    }

    private IEnumerator Co_GuardCool(float duration)
    {
        SetState(GuardState.Cooldown);

        float elapsed = 0f;
        slider.value = 0f;

        while(elapsed < duration) 
        {
            elapsed += Time.deltaTime;
            slider.value = elapsed / duration;
            yield return null;
        }

        slider.value = 1f;
        SetState(GuardState.Normal);
        cooldownRoutine = null;
    }

    public void UpdateGauge(float value)
    {
        if (slider != null) slider.value = value;

        // 상태 자동 전환
        if (value >= 1f && curState == GuardState.Cooldown)
        {
            SetState(GuardState.Normal);
        }
        else if (value < 1f && curState == GuardState.Normal)
        {
            SetState(GuardState.Cooldown);
        }
    }

    public void PlayPerfect()
    {
        transform.DOPunchScale(Vector3.one * 0.4f, 0.3f, 10, 1f);

        fImage.DOColor(Color.white, 0.05f).OnComplete(() =>
        {
            fImage.DOColor(activeColor, 0.15f);
        });
    }

    private void ResetAnim()
    {
        pulseTween?.Kill();
        transform.DOKill();
        fImage.transform.DOKill();
        transform.localScale = Vector3.one;
        fImage.transform.localScale = Vector3.one;
    }
}
