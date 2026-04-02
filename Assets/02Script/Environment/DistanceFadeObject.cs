using DG.Tweening;
using UnityEngine;

public class DistanceFadeObject : MonoBehaviour
{
    [Header("Target")]
    [SerializeField] private Transform target;
    [SerializeField] private SpriteRenderer[] targetRenderers;
    [SerializeField] private Collider2D[] targetColliders;

    [Header("Distance")]
    [SerializeField] private float visibleDistance = 6f;
    [SerializeField] private float hiddenDistance = 8f;
    [SerializeField] private bool use2DDistanceOnly = true;

    [Header("Fade")]
    [SerializeField] private float fadeDuration = 0.35f;
    [SerializeField] private Ease fadeEase = Ease.OutQuad;
    [SerializeField] private float hiddenAlpha = 0f;
    [SerializeField] private bool disableCollidersWhenHidden = true;

    private Tween fadeTween;
    private float currentAlpha = 1f;
    private bool isVisible = true;
    private Color[] defaultRendererColors;

    private void Reset()
    {
        CacheTargets();
    }

    private void Awake()
    {
        CacheTargets();
        ResolveTarget();
        CacheDefaultRendererColors();
        ApplyAlphaImmediate(currentAlpha);
    }

    private void Start()
    {
        UpdateVisibility(true);
    }

    private void Update()
    {
        if (target == null)
            ResolveTarget();

        if (target == null)
            return;

        UpdateVisibility(false);
    }

    private void OnDisable()
    {
        fadeTween?.Kill();
        fadeTween = null;
    }

    private void OnValidate()
    {
        visibleDistance = Mathf.Max(0f, visibleDistance);
        hiddenDistance = Mathf.Max(visibleDistance, hiddenDistance);
        fadeDuration = Mathf.Max(0f, fadeDuration);
        hiddenAlpha = Mathf.Clamp01(hiddenAlpha);
    }

    private void CacheTargets()
    {
        if (targetRenderers == null || targetRenderers.Length == 0)
            targetRenderers = GetComponentsInChildren<SpriteRenderer>(true);

        if (targetColliders == null || targetColliders.Length == 0)
            targetColliders = GetComponentsInChildren<Collider2D>(true);
    }

    private void CacheDefaultRendererColors()
    {
        if (targetRenderers == null)
        {
            defaultRendererColors = System.Array.Empty<Color>();
            return;
        }

        defaultRendererColors = new Color[targetRenderers.Length];
        for (int i = 0; i < targetRenderers.Length; i++)
        {
            SpriteRenderer renderer = targetRenderers[i];
            defaultRendererColors[i] = renderer != null ? renderer.color : Color.white;
        }
    }

    private void ResolveTarget()
    {
        if (target != null)
            return;

        if (Player.Instance != null)
        {
            target = Player.Instance.transform;
            return;
        }

        Player foundPlayer = FindFirstObjectByType<Player>();
        if (foundPlayer != null)
            target = foundPlayer.transform;
    }

    private void UpdateVisibility(bool instant)
    {
        float distance = GetDistanceToTarget();
        bool shouldBeVisible = isVisible
            ? distance <= hiddenDistance
            : distance <= visibleDistance;

        if (shouldBeVisible == isVisible && !instant)
            return;

        SetVisible(shouldBeVisible, instant);
    }

    private float GetDistanceToTarget()
    {
        Vector3 from = GetFadeOrigin();
        Vector3 to = target.position;

        if (use2DDistanceOnly)
        {
            from.z = 0f;
            to.z = 0f;
        }

        return Vector3.Distance(from, to);
    }

    private Vector3 GetFadeOrigin()
    {
        if (TryGetCombinedRendererCenter(out Vector3 rendererCenter))
            return rendererCenter;

        if (TryGetCombinedColliderCenter(out Vector3 colliderCenter))
            return colliderCenter;

        return transform.position;
    }

    private bool TryGetCombinedRendererCenter(out Vector3 center)
    {
        center = default;

        if (targetRenderers == null || targetRenderers.Length == 0)
            return false;

        bool hasBounds = false;
        Bounds combinedBounds = default;

        for (int i = 0; i < targetRenderers.Length; i++)
        {
            SpriteRenderer renderer = targetRenderers[i];
            if (renderer == null)
                continue;

            if (!hasBounds)
            {
                combinedBounds = renderer.bounds;
                hasBounds = true;
            }
            else
                combinedBounds.Encapsulate(renderer.bounds);
        }

        if (!hasBounds)
            return false;

        center = combinedBounds.center;
        return true;
    }

    private bool TryGetCombinedColliderCenter(out Vector3 center)
    {
        center = default;

        if (targetColliders == null || targetColliders.Length == 0)
            return false;

        bool hasBounds = false;
        Bounds combinedBounds = default;

        for (int i = 0; i < targetColliders.Length; i++)
        {
            Collider2D colliderTarget = targetColliders[i];
            if (colliderTarget == null)
                continue;

            if (!hasBounds)
            {
                combinedBounds = colliderTarget.bounds;
                hasBounds = true;
            }
            else
                combinedBounds.Encapsulate(colliderTarget.bounds);
        }

        if (!hasBounds)
            return false;

        center = combinedBounds.center;
        return true;
    }

    private void SetVisible(bool visible, bool instant)
    {
        isVisible = visible;
        float targetAlpha = visible ? 1f : hiddenAlpha;

        fadeTween?.Kill();

        if (visible || !disableCollidersWhenHidden)
            SetCollidersEnabled(true);

        if (instant || fadeDuration <= 0f)
        {
            ApplyAlphaImmediate(targetAlpha);
            if (!visible && disableCollidersWhenHidden)
                SetCollidersEnabled(false);
            return;
        }

        fadeTween = DOTween.To(
                () => currentAlpha,
                value => ApplyAlphaImmediate(value),
                targetAlpha,
                fadeDuration
            )
            .SetEase(fadeEase)
            .SetTarget(this)
            .OnComplete(() =>
            {
                if (!visible && disableCollidersWhenHidden)
                    SetCollidersEnabled(false);
            });
    }

    private void ApplyAlphaImmediate(float alpha)
    {
        currentAlpha = alpha;

        if (targetRenderers == null)
            return;

        for (int i = 0; i < targetRenderers.Length; i++)
        {
            SpriteRenderer renderer = targetRenderers[i];
            if (renderer == null)
                continue;

            Color color = i < defaultRendererColors.Length
                ? defaultRendererColors[i]
                : renderer.color;
            color.a *= alpha;
            renderer.color = color;
        }
    }

    private void SetCollidersEnabled(bool enabled)
    {
        if (targetColliders == null)
            return;

        for (int i = 0; i < targetColliders.Length; i++)
        {
            Collider2D colliderTarget = targetColliders[i];
            if (colliderTarget == null)
                continue;

            colliderTarget.enabled = enabled;
        }
    }
}
