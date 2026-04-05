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


    private void Awake()
    {
        if (topBox != null) topRect = topBox.rectTransform;
        if (bottomBox != null) bottomRect = bottomBox.rectTransform;
    }

    private void Start()
    {
        PlayLetterboxIn();
    }

    public void Init()
    {

    }

    public void PlayLetterboxIn()
    {
        // 레터박스 차오르는 연출
        StartCoroutine(Co_Letterbox(targetHeight, letterboxDuration));
    }

    public void PlayLetterboxOut()
    {
        // 레터박스 사라지는 연출
        StartCoroutine(Co_Letterbox(0, letterboxDuration));
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

            float currentHeight = Mathf.Lerp(startHeight, endHeight, t);
            Vector2 topSize = topRect.sizeDelta;
            topSize.y = currentHeight;
            topRect.sizeDelta = topSize;

            Vector2 bottomSize = bottomRect.sizeDelta;
            bottomSize.y = currentHeight;
            bottomRect.sizeDelta = bottomSize;
        }
    }

}