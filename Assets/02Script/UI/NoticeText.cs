using System.Collections;
using TMPro;
using UnityEngine;

public class NoticeText : MonoBehaviour
{
    [SerializeField] private TextMeshProUGUI text;
    [SerializeField] private float displayDuration = 2f;
    [SerializeField] private float fadeDuration = 0.4f;

    private Coroutine _routine;

    public void Show(string message)
    {
        gameObject.SetActive(true);
        if (_routine != null) StopCoroutine(_routine);
        _routine = StartCoroutine(ShowRoutine(message));
    }

    private IEnumerator ShowRoutine(string message)
    {
        text.text = message;

        SetAlpha(1f);

        yield return new WaitForSeconds(displayDuration);

        // 서서히 사라짐
        float elapsed = 0f;
        while (elapsed < fadeDuration)
        {
            elapsed += Time.deltaTime;
            SetAlpha(1f - Mathf.Clamp01(elapsed / fadeDuration));
            yield return null;
        }

        gameObject.SetActive(false);
        _routine = null;
    }

    private void SetAlpha(float a)
    {
        Color c = text.color;
        c.a = a;
        text.color = c;
    }
}
