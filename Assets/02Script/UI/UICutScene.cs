using System.Collections;
using UnityEngine;
using UnityEngine.UI;


public class UICutScene : MonoBehaviour
{
    [Header("Letter Box")]
    [SerializeField] private Image topBox;
    private RectTransform topRect;
    [SerializeField] private Image bottomBox;
    private RectTransform bottomRect;

    [Header("Cutscene Settings")]
    [Tooltip("레터박스 차오르는 시간"), SerializeField] private float letterboxDuration;
    [Tooltip("레터박스 차오르는 높이"), SerializeField] private float targetHeight;
    private Coroutine letterboxRoutine;

    [Header("Boss")]
    [SerializeField] private CanvasGroup bossPanel;
    [SerializeField] private Text titleText;
    [SerializeField] private Text nameText;
    [SerializeField] private float bossDuration;
    [SerializeField] private float showDuration;


    private void Awake()
    {
        if (topBox != null) topRect = topBox.rectTransform;
        if (bottomBox != null) bottomRect = bottomBox.rectTransform;
    }

    public void Init()
    {
        if (topRect == null || bottomRect == null) return;

        SetLetterbox(topRect, 0);
        SetLetterbox(bottomRect, 0);
    }

    #region Letter Box
    private void SetLetterbox(RectTransform rect, float height)
    {
        if (rect == null) return;

        // 레터박스 사이즈 지정
        Vector2 size = rect.sizeDelta;
        size.y = height;
        rect.sizeDelta = size;
    }

    private IEnumerator Co_Letterbox(float endHeight, float duration)
    {
        if (topRect == null || bottomRect == null) yield break;

        float startHeight = topRect.sizeDelta.y;
        float elapsed = 0f;

        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / duration);
            float easeT = 1f - (1f - t) * (1f - t);

            float currentHeight = Mathf.Lerp(startHeight, endHeight, easeT);
            SetLetterbox(topRect, currentHeight);
            SetLetterbox(bottomRect, currentHeight);

            yield return null;
        }

        SetLetterbox(topRect, endHeight);
        SetLetterbox(bottomRect, endHeight);
        letterboxRoutine = null;
    }
    

    public void PlayLetterboxIn()
    {
        if (letterboxRoutine != null) StopCoroutine(letterboxRoutine);

        // 레터박스 차오르는 연출
        letterboxRoutine = StartCoroutine(Co_Letterbox(targetHeight, letterboxDuration));

        Debug.Log("[UI] Letterbox In");
    }

    public void PlayLetterboxOut()
    {
        if (letterboxRoutine != null) StopCoroutine(letterboxRoutine);

        // 레터박스 사라지는 연출
        letterboxRoutine = StartCoroutine(Co_Letterbox(0, letterboxDuration));

        Debug.Log("[UI] Letterbox Out");
    }
    #endregion

    #region Boss
    public void PlayBossName(string name, string title)
    {
        if (bossPanel == null) return;
        if (titleText == null || nameText == null) return;

        titleText.text = title;
        nameText.text = name;

        StartCoroutine(Co_BossName());
    }

    private IEnumerator Co_BossName()
    {
        float currentAlpha = bossPanel.alpha;
        float targetAlpha = 1;
        float elapsed = 0f;

        // 보스 이름 페이드 인
        while (elapsed < bossDuration)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / bossDuration);
            float easeT = 1f - (1f - t) * (1f - t);

            bossPanel.alpha = Mathf.Lerp(currentAlpha, targetAlpha, easeT);

            yield return null;
        }
        bossPanel.alpha = targetAlpha;
        
        // 보스 이름 보여주는 시간
        yield return new WaitForSecondsRealtime(showDuration);

        currentAlpha = bossPanel.alpha;
        targetAlpha = 0;
        elapsed = 0f;

        // 보스 이름 페이드 아웃
        while (elapsed < bossDuration)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / bossDuration);
            float easeT = 1f - (1f - t) * (1f - t);

            bossPanel.alpha = Mathf.Lerp(currentAlpha, targetAlpha, easeT);

            yield return null;
        }

        bossPanel.alpha = targetAlpha;
    }
    #endregion
}