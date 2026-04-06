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

    #region Core
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
    #endregion 

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

}