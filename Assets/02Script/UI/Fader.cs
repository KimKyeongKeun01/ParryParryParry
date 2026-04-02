using UnityEngine;
using UnityEngine.UI;
using System.Collections;
using System;

public class Fader : MonoBehaviour
{
    [Header("Fade Setting")]
    [SerializeField] private float defaultFadeInDuration = 0.5f;
    [SerializeField] private float defaultFadeOutDuration = 0.5f;
    [SerializeField] private float defaultHoldTime = 0.3f;
    private Coroutine fadeCoroutine;

    public Image fadeImage;
    [SerializeField] private Color fadeColor = Color.black;

    

    private void Awake() => Initialize();

    private void Initialize()
    {
        fadeImage.raycastTarget = false;
        SetColor(fadeColor);
        SetAlpha(0f);
    }

    public void FadeIn(float duration = -1f, Action onComplete = null)
    {
        if (fadeImage == null) return;
        
        // Fade Duration 미 입력시 default 값 사용
        float time = (duration < 0f) ? defaultFadeInDuration : duration;

        // Fade 실행
        StopCurrentFade();
        fadeCoroutine = StartCoroutine(FadeRoutine(1f, 0f, time, onComplete));
    }

    public void FadeOut(float duration = -1f, Action onComplete = null)
    {
        if (fadeImage == null) return;

        // Fade Duration 미 입력시 default 값 사용
        float time = (duration < 0f) ? defaultFadeOutDuration : duration;

        // Fade 실행
        StopCurrentFade();
        fadeCoroutine = StartCoroutine(FadeRoutine(0f, 1f, time, onComplete));
    }

    public void FadeInOut(float fadeInDuration = -1f, float holdTime = -1f, float fadeOutDuration = -1f, Action onMidComplete = null, Action onComplete = null)
    {
        if (fadeImage == null) return;

        // Fade Duration 미 입력시 default 값 사용
        float fIn = (fadeInDuration < 0f) ? defaultFadeInDuration : fadeInDuration;
        float fOut = (fadeOutDuration < 0f) ? defaultFadeOutDuration : fadeOutDuration;
        float hTime = (holdTime < 0f) ? defaultHoldTime : holdTime;

        // Fade 실행
        StopCurrentFade();
        fadeCoroutine = StartCoroutine(FadeInOutRoutine(fIn, hTime, fOut, onMidComplete, onComplete));
    }

    private IEnumerator FadeInOutRoutine(float fadeInDuration, float holdTime, float fadeOutDuration, Action onMidComplete, Action onComplete)
    {
        yield return FadeRoutine(0f, 1f, fadeInDuration, null);

        onMidComplete?.Invoke();

        if (holdTime > 0f)
            yield return new WaitForSeconds(holdTime);

        yield return FadeRoutine(1f, 0f, fadeOutDuration, null);

        fadeCoroutine = null;
        onComplete?.Invoke();
    }

    private IEnumerator FadeRoutine(float startAlpha, float endAlpha, float duration, Action onComplete = null)
    {
        if (duration <= 0f)
        {
            SetAlpha(endAlpha);
            fadeCoroutine = null;
            onComplete?.Invoke();
            yield break;
        }

        SetAlpha(startAlpha);
        float elapsed = 0f;
        Color color = fadeImage.color;

        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / duration);
            color.a = Mathf.Lerp(startAlpha, endAlpha, t);
            fadeImage.color = color;
            yield return null;
        }

        color.a = endAlpha;
        fadeImage.color = color;

        fadeCoroutine = null;
        onComplete?.Invoke();
        fadeImage.raycastTarget = endAlpha == 0 ? false : true;
    }

    private void StopCurrentFade()
    {
        if (fadeCoroutine != null)
        {
            StopCoroutine(fadeCoroutine);
            fadeCoroutine = null;
        }
    }

    private void SetAlpha(float alpha)
    {
        if (fadeImage == null) return;

        Color color = fadeImage.color;
        color.a = alpha;
        fadeImage.color = color;
    }

    public void SetFadeColor(Color color) => SetColor(color);

    private void SetColor(Color color)
    {
        if (fadeImage == null) return;

        fadeImage.color = color;
    }
}
