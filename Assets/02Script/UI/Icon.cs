using DG.Tweening;
using UnityEngine;
using UnityEngine.UI;

public class Icon : MonoBehaviour
{
    private Image image;
    [SerializeField] private bool? isFull = null;
    private Tween idleTween;
    [SerializeField] private Vector3 originScale;
    [SerializeField] private Vector3 idleScale;
    [SerializeField] private Vector3 shakeScale;

    private void Awake()
    {
        image = GetComponent<Image>();
        originScale = image.transform.localScale;
    }

    private void Start()
    {
        PlayIdle();
    }

    public void SetState(bool nextIsFull, Sprite full, Sprite empty, bool force = false)
    {
        if (force || isFull != nextIsFull)
        {
            isFull = nextIsFull;

            // 기존 애니메이션 정지
            idleTween?.Kill();
            transform.DOKill();

            if (isFull == true)
            {
                image.sprite = full;
                transform.localScale = Vector3.zero;
                transform.DOScale(originScale, 0.5f).SetEase(Ease.OutBack).OnComplete(() => PlayIdle());
            }
            else
            {
                image.sprite = empty;

                transform.DOShakePosition(0.2f, 5f, 20);
                transform.DOScale(originScale * 0.8f, 0.1f).OnComplete(() =>
                {
                    transform.DOScale(originScale, 0.2f).SetEase(Ease.OutBounce).OnComplete(() => PlayIdle());
                });
            }
        }
    }

    private void PlayIdle()
    {
        // 뽀용뽀용
        idleTween?.Kill();

        float delay = Random.Range(0f, 0.5f);
        float time = 0.8f + Random.Range(-0.1f, 0.1f);

        idleTween = transform.DOScale(originScale * 1.1f, time).SetLoops(-1, LoopType.Yoyo).SetEase(Ease.InOutSine).SetDelay(delay);
    }

    private void OnDestroy()
    {
        idleTween?.Kill();
        transform.DOKill();
    }
}
