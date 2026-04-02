using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class Platform_Crumbling : MonoBehaviour
{
    private const float TopContactNormalThreshold = 0.5f;
    private const float TopContactVerticalTolerance = 0.1f;
    #region 타입 정의
    public enum PlatformState
    {
        Visible,
        Hidden
    }

    public enum TriggerMode
    {
        AutomaticInterval,
        PlayerStepDelay,
        ToggleOnJump,
        ExternalTrigger
    }

    public enum RespawnMode
    {
        Never,
        AfterDelay
    }
    #endregion

    #region 인스펙터 설정
    [SerializeField] private PlatformState currentState = PlatformState.Visible;
    [SerializeField] private TriggerMode triggerMode = TriggerMode.PlayerStepDelay;
    [SerializeField] private RespawnMode respawnMode = RespawnMode.AfterDelay;
    [SerializeField] private string playerTag = "Player";
    [SerializeField] private float automaticInterval = 1f;
    [SerializeField] private float playerStepDelay = 0.5f;
    [SerializeField] private float respawnDelay = 2f;
    [SerializeField] private Collider2D platformCollider;
    [SerializeField] private Collider2D respawnBlockCollider;
    [SerializeField] private SpriteRenderer platformRenderer;
    [SerializeField] private bool useFadeLerp = false;
    [SerializeField] private float fadeDuration = 0.2f;
    #endregion

    #region 내부 상태
    private readonly HashSet<Transform> playersOnPlatform = new HashSet<Transform>();
    private readonly List<Collider2D> respawnOverlapResults = new List<Collider2D>(8);

    private Coroutine automaticRoutine;
    private Coroutine playerStepRoutine;
    private Coroutine respawnRoutine;
    private Coroutine fadeRoutine;
    private bool pendingToggleOnJumpShow;
    #endregion

    #region 유니티 생명주기
    private void Awake()
    {
        SyncCapsuleColliderDirections();

        if (platformCollider == null)
        {
            platformCollider = FindPlatformCollider();
        }

        if (respawnBlockCollider == null)
        {
            respawnBlockCollider = FindRespawnBlockCollider();
        }

        if (platformRenderer == null)
        {
            platformRenderer = GetComponent<SpriteRenderer>();
        }
    }

    private void Start()
    {
        ApplyState();

        if (triggerMode == TriggerMode.AutomaticInterval)
        {
            automaticRoutine = StartCoroutine(AutomaticToggleRoutine());
        }
    }

    private void OnEnable()
    {
        PlayerController.JumpStarted += HandlePlayerJumpStarted;
    }

    private void OnDisable()
    {
        PlayerController.JumpStarted -= HandlePlayerJumpStarted;
        StopManagedCoroutine(ref automaticRoutine);
        StopManagedCoroutine(ref playerStepRoutine);
        StopManagedCoroutine(ref respawnRoutine);
        StopManagedCoroutine(ref fadeRoutine);
        pendingToggleOnJumpShow = false;
        playersOnPlatform.Clear();
    }

    private void Update()
    {
        if (!pendingToggleOnJumpShow ||
            triggerMode != TriggerMode.ToggleOnJump ||
            currentState != PlatformState.Hidden ||
            IsAnyPlayerOverlappingRespawnArea())
        {
            return;
        }

        ShowPlatform();
    }

    private void OnValidate()
    {
        SyncCapsuleColliderDirections();

        if (platformCollider == null)
        {
            platformCollider = FindPlatformCollider();
        }

        if (respawnBlockCollider == null)
        {
            respawnBlockCollider = FindRespawnBlockCollider();
        }

        if (automaticInterval < 0f)
        {
            automaticInterval = 0f;
        }

        if (playerStepDelay < 0f)
        {
            playerStepDelay = 0f;
        }

        if (respawnDelay < 0f)
        {
            respawnDelay = 0f;
        }

        if (fadeDuration < 0f)
        {
            fadeDuration = 0f;
        }
    }
    #endregion

    #region 상태 전환
    public void SetState(PlatformState newState)
    {
        if (newState == PlatformState.Visible)
        {
            ShowPlatform();
        }
        else
        {
            HidePlatform();
        }
    }

    public void HidePlatform()
    {
        HidePlatform(true);
    }

    public void ShowPlatform()
    {
        if (currentState == PlatformState.Visible)
        {
            return;
        }

        if (IsAnyPlayerOverlappingRespawnArea())
        {
            return;
        }

        currentState = PlatformState.Visible;
        pendingToggleOnJumpShow = false;

        StopManagedCoroutine(ref playerStepRoutine);
        StopManagedCoroutine(ref respawnRoutine);
        StopManagedCoroutine(ref fadeRoutine);

        if (!useFadeLerp)
        {
            ApplyState();
            return;
        }

        if (platformCollider != null)
        {
            platformCollider.enabled = true;
        }

        if (platformRenderer != null)
        {
            platformRenderer.enabled = true;
        }

        fadeRoutine = StartCoroutine(FadeRendererRoutine(1f, false));
    }

    public void ToggleState()
    {
        ToggleState(true);
    }

    public void TriggerPlatform()
    {
        ToggleState();
    }

    private void HidePlatform(bool allowRespawn)
    {
        if (currentState == PlatformState.Hidden)
        {
            return;
        }

        currentState = PlatformState.Hidden;
        pendingToggleOnJumpShow = false;

        StopManagedCoroutine(ref playerStepRoutine);
        StopManagedCoroutine(ref respawnRoutine);
        StopManagedCoroutine(ref fadeRoutine);
        playersOnPlatform.Clear();

        if (!useFadeLerp)
        {
            ApplyState();
        }
        else
        {
            if (platformCollider != null)
            {
                platformCollider.enabled = false;
            }

            fadeRoutine = StartCoroutine(FadeRendererRoutine(0f, true));
        }

        if (allowRespawn && respawnMode == RespawnMode.AfterDelay)
        {
            respawnRoutine = StartCoroutine(RespawnRoutine());
        }
    }

    private void ToggleState(bool allowRespawn)
    {
        if (currentState == PlatformState.Visible)
        {
            HidePlatform(allowRespawn);
        }
        else
        {
            ShowPlatform();
        }
    }

    private void ApplyState()
    {
        bool isVisible = currentState == PlatformState.Visible;

        if (platformCollider != null)
        {
            platformCollider.enabled = isVisible;
        }

        if (platformRenderer != null)
        {
            platformRenderer.enabled = useFadeLerp || isVisible;

            Color color = platformRenderer.color;
            color.a = isVisible ? 1f : 0f;
            platformRenderer.color = color;
        }
    }
    #endregion

    #region 충돌 감지
    private void OnCollisionEnter2D(Collision2D collision)
    {
        HandlePlayerEnter(collision);
    }

    private void OnCollisionExit2D(Collision2D collision)
    {
        HandlePlayerExit(collision);
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        HandlePlayerEnter(other);
    }

    private void OnCollisionStay2D(Collision2D collision)
    {
        HandlePlayerEnter(collision);
    }

    private void OnTriggerStay2D(Collider2D other)
    {
        HandlePlayerEnter(other);
    }

    private void OnTriggerExit2D(Collider2D other)
    {
        HandlePlayerExit(other);
    }
    #endregion

    #region 트리거 처리
    private void HandlePlayerEnter(Collision2D collision)
    {
        if (!HasTopContact(collision) ||
            !TryGetPlayer(collision.collider, out Transform playerRoot, out Rigidbody2D playerBody))
        {
            return;
        }

        RegisterPlayerOnPlatform(playerRoot);
    }

    private void HandlePlayerEnter(Collider2D other)
    {
        if (!IsTopTriggerContact(other) ||
            !TryGetPlayer(other, out Transform playerRoot, out Rigidbody2D playerBody))
        {
            return;
        }

        RegisterPlayerOnPlatform(playerRoot);
    }

    private void HandlePlayerExit(Collision2D collision)
    {
        if (!TryGetPlayer(collision.collider, out Transform playerRoot, out Rigidbody2D playerBody))
        {
            return;
        }

        playersOnPlatform.Remove(playerRoot);
    }

    private void HandlePlayerExit(Collider2D other)
    {
        if (!TryGetPlayer(other, out Transform playerRoot, out Rigidbody2D playerBody))
        {
            return;
        }

        playersOnPlatform.Remove(playerRoot);
    }

    private IEnumerator AutomaticToggleRoutine()
    {
        while (enabled && triggerMode == TriggerMode.AutomaticInterval)
        {
            yield return new WaitForSeconds(automaticInterval);
            ToggleState(false);
        }
    }

    private IEnumerator PlayerStepDelayRoutine()
    {
        yield return new WaitForSeconds(playerStepDelay);
        playerStepRoutine = null;

        if (currentState == PlatformState.Visible)
        {
            HidePlatform();
        }
    }

    private IEnumerator RespawnRoutine()
    {
        yield return new WaitForSeconds(respawnDelay);

        while (IsAnyPlayerOverlappingRespawnArea())
        {
            yield return null;
        }

        respawnRoutine = null;
        ShowPlatform();
    }

    private IEnumerator FadeRendererRoutine(float targetAlpha, bool disableColliderAfterFade)
    {
        if (platformRenderer == null)
        {
            if (disableColliderAfterFade && platformCollider != null)
            {
                platformCollider.enabled = false;
            }

            fadeRoutine = null;
            yield break;
        }

        platformRenderer.enabled = true;

        Color color = platformRenderer.color;
        float startAlpha = color.a;

        if (fadeDuration <= 0f)
        {
            color.a = targetAlpha;
            platformRenderer.color = color;
        }
        else
        {
            float elapsed = 0f;

            while (elapsed < fadeDuration)
            {
                elapsed += Time.deltaTime;
                color.a = Mathf.Lerp(startAlpha, targetAlpha, elapsed / fadeDuration);
                platformRenderer.color = color;
                yield return null;
            }

            color.a = targetAlpha;
            platformRenderer.color = color;
        }

        if (disableColliderAfterFade && platformCollider != null)
        {
            platformCollider.enabled = false;
        }

        if (targetAlpha <= 0f && platformRenderer != null)
        {
            platformRenderer.enabled = false;
        }

        fadeRoutine = null;
    }
    #endregion

    #region 판정 및 유틸
    private void HandlePlayerJumpStarted(PlayerController controller)
    {
        if (controller == null ||
            triggerMode != TriggerMode.ToggleOnJump)
        {
            return;
        }

        if (currentState == PlatformState.Visible)
        {
            HidePlatform(false);
            return;
        }

        if (IsAnyPlayerOverlappingRespawnArea())
        {
            pendingToggleOnJumpShow = true;
            return;
        }

        ShowPlatform();
    }

    private void RegisterPlayerOnPlatform(Transform playerRoot)
    {
        if (!playersOnPlatform.Add(playerRoot))
        {
            return;
        }

        if (triggerMode == TriggerMode.PlayerStepDelay &&
            currentState == PlatformState.Visible &&
            playerStepRoutine == null)
        {
            playerStepRoutine = StartCoroutine(PlayerStepDelayRoutine());
        }
    }

    private bool HasTopContact(Collision2D collision)
    {
        for (int i = 0; i < collision.contactCount; i++)
        {
            ContactPoint2D contact = collision.GetContact(i);
            if (contact.normal.y >= TopContactNormalThreshold)
            {
                return true;
            }

            if (contact.point.y >= platformCollider.bounds.max.y - TopContactVerticalTolerance)
            {
                return true;
            }
        }

        return false;
    }

    private bool IsTopTriggerContact(Collider2D other)
    {
        if (platformCollider == null)
        {
            return false;
        }

        Bounds platformBounds = platformCollider.bounds;
        Bounds otherBounds = other.bounds;
        float verticalGap = otherBounds.min.y - platformBounds.max.y;
        bool overlapsHorizontally = otherBounds.max.x > platformBounds.min.x &&
                                    otherBounds.min.x < platformBounds.max.x;

        return overlapsHorizontally && verticalGap >= -TopContactVerticalTolerance;
    }

    private bool IsAnyPlayerOverlappingRespawnArea()
    {
        Collider2D overlapSource = GetRespawnBlockCollider();
        if (overlapSource == null)
        {
            return false;
        }

        respawnOverlapResults.Clear();
        ContactFilter2D contactFilter = default;
        contactFilter.useTriggers = true;
        overlapSource.Overlap(contactFilter, respawnOverlapResults);

        for (int i = 0; i < respawnOverlapResults.Count; i++)
        {
            Collider2D overlap = respawnOverlapResults[i];
            if (overlap == null || overlap == overlapSource)
            {
                continue;
            }

            if (TryGetPlayer(overlap, out Transform playerRoot, out Rigidbody2D playerBody))
            {
                return true;
            }
        }

        return false;
    }

    private Collider2D GetRespawnBlockCollider()
    {
        if (respawnBlockCollider != null)
        {
            return respawnBlockCollider;
        }

        respawnBlockCollider = FindRespawnBlockCollider();
        return respawnBlockCollider;
    }

    private void SyncCapsuleColliderDirections()
    {
        PlatformContactUtility2D.SyncCapsuleDirections(GetComponents<Collider2D>());
    }

    private Collider2D FindRespawnBlockCollider()
    {
        Collider2D[] colliders = GetComponents<Collider2D>();
        Collider2D triggerFallback = null;

        for (int i = 0; i < colliders.Length; i++)
        {
            Collider2D candidate = colliders[i];
            if (candidate == null || candidate == platformCollider || !candidate.isTrigger)
            {
                continue;
            }

            if (candidate is CapsuleCollider2D)
            {
                return candidate;
            }

            if (triggerFallback == null)
            {
                triggerFallback = candidate;
            }
        }

        if (triggerFallback != null)
        {
            return triggerFallback;
        }

        return platformCollider;
    }

    private Collider2D FindPlatformCollider()
    {
        Collider2D[] colliders = GetComponents<Collider2D>();
        Collider2D fallbackCollider = null;

        for (int i = 0; i < colliders.Length; i++)
        {
            Collider2D candidate = colliders[i];
            if (candidate == null || candidate.isTrigger)
            {
                continue;
            }

            if (candidate is BoxCollider2D)
            {
                return candidate;
            }

            if (fallbackCollider == null)
            {
                fallbackCollider = candidate;
            }
        }

        return fallbackCollider;
    }

    private bool TryGetPlayer(Collider2D other, out Transform playerRoot, out Rigidbody2D playerBody)
    {
        return PlatformContactUtility2D.TryResolveTaggedTarget(other, playerTag, out playerRoot, out playerBody);
    }

    private void StopManagedCoroutine(ref Coroutine routine)
    {
        if (routine == null)
        {
            return;
        }

        StopCoroutine(routine);
        routine = null;
    }
    #endregion
}
