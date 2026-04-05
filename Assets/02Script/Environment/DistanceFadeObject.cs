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

    // Gizmos
    [SerializeField] private float gizmoOriginSphereRadius = 0.12f;
    [SerializeField] private int gizmoCircleSegments = 48;

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

    #region
    private void OnDrawGizmosSelected()
    {
        // 에디터에서 바로 보이게 target/renderer/collider 캐시를 최대한 보완
        CacheTargets();

        Vector3 origin = GetFadeOrigin();

        // 기준점 표시
        Gizmos.color = Color.cyan;
        Gizmos.DrawSphere(origin, gizmoOriginSphereRadius);

        // 2D/3D 거리 모드에 따라 거리 반경 표시
        if (use2DDistanceOnly)
        {
            DrawWireCircle2D(origin, visibleDistance, new Color(0.2f, 1f, 0.2f, 1f));
            DrawWireCircle2D(origin, hiddenDistance, new Color(1f, 0.5f, 0.1f, 1f));
        }
        else
        {
            Gizmos.color = new Color(0.2f, 1f, 0.2f, 1f);
            Gizmos.DrawWireSphere(origin, visibleDistance);

            Gizmos.color = new Color(1f, 0.5f, 0.1f, 1f);
            Gizmos.DrawWireSphere(origin, hiddenDistance);
        }

        // 현재 타겟과의 연결선, 현재 거리 시각화
        if (target != null)
        {
            Vector3 targetPos = target.position;

            if (use2DDistanceOnly)
            {
                targetPos.z = origin.z;
            }

            float currentDistance = GetDistanceForGizmo(origin, targetPos);
            bool currentlyVisible = isVisible
                ? currentDistance <= hiddenDistance
                : currentDistance <= visibleDistance;

            Gizmos.color = currentlyVisible ? Color.green : Color.red;
            Gizmos.DrawLine(origin, targetPos);

            Gizmos.color = Color.yellow;
            Gizmos.DrawSphere(targetPos, gizmoOriginSphereRadius * 0.85f);
        }
    }

    /// <summary>
    /// XY 평면 기준 2D 원형 기즈모.
    /// 2D 프로젝트에서 visible/hidden 거리 반경을 직관적으로 보기 좋다.
    /// </summary>
    private void DrawWireCircle2D(Vector3 center, float radius, Color color)
    {
        if (radius <= 0f)
            return;

        int segments = Mathf.Max(8, gizmoCircleSegments);
        Gizmos.color = color;

        float angleStep = 360f / segments;
        Vector3 prevPoint = center + new Vector3(radius, 0f, 0f);

        for (int i = 1; i <= segments; i++)
        {
            float angle = angleStep * i * Mathf.Deg2Rad;
            Vector3 nextPoint = center + new Vector3(Mathf.Cos(angle) * radius, Mathf.Sin(angle) * radius, 0f);
            Gizmos.DrawLine(prevPoint, nextPoint);
            prevPoint = nextPoint;
        }
    }

    /// <summary>
    /// 기즈모용 현재 거리 계산.
    /// 런타임 로직의 GetDistanceToTarget()과 같은 기준으로 맞춘다.
    /// </summary>
    private float GetDistanceForGizmo(Vector3 from, Vector3 to)
    {
        if (use2DDistanceOnly)
        {
            from.z = 0f;
            to.z = 0f;
        }

        return Vector3.Distance(from, to);
    }
#endregion
}
