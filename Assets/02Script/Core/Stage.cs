using System;
using System.Collections.Generic;
using Unity.Cinemachine;
using UnityEngine;

[RequireComponent(typeof(BoxCollider2D))]
[ExecuteAlways]
[DisallowMultipleComponent]
public class Stage : MonoBehaviour
{
    public int Index { get; private set; }

    [SerializeField] private CinemachineCamera virtualCam;
    [SerializeField] private CinemachineImpulseListener impulseListener;

    [SerializeField] private Vector2 triggerPadding = new(2f, 25f);
    [SerializeField] private float cameraHorizontalPadding = 5f;
    [SerializeField] private float cameraTopPadding = 10f;
    [SerializeField] private float cameraBottomPadding = 0f;
    [Tooltip("계산된 OrthographicSize가 이 값을 초과하면 맵 전체 고정 대신 플레이어를 따라가는 크기로 고정")]
    [SerializeField] private float maxFollowOrthoSize = 12f;
    [Tooltip("체크 시 세로 기준, 해제 시 가로 기준으로 OrthographicSize 계산")]
    [SerializeField] private bool fitByHeight = false;

    [Header("Save Points")]
    [SerializeField] private Transform beforeClearSavePoint;
    [SerializeField] private Transform afterClearSavePoint;

    [Header("전환 효과")]
    [SerializeField] private bool useFadeTransition = false;
    [SerializeField] private float fadeDuration = 0.3f;
    [SerializeField] private float fadeHoldDuration = 0f;
    [SerializeField] private Color fadeColor = Color.black;

    [Header("적")]
    [SerializeField] private List<GameObject> enemies = new();

    [Header("허용 기술")]
    [SerializeField] private PlayerAbilityFlags allowedAbilities = PlayerAbilityFlags.All;

    public CinemachineCamera VirtualCamera => virtualCam;
    public CinemachineImpulseListener ImpulseListener => impulseListener;
    public Transform BeforeClearSavePoint => beforeClearSavePoint;
    public Transform AfterClearSavePoint => afterClearSavePoint;
    public PlayerAbilityFlags AllowedAbilities => allowedAbilities;
    public bool UseFadeTransition => useFadeTransition;
    public float FadeDuration => fadeDuration;
    public float FadeHoldDuration => fadeHoldDuration;
    public Color FadeColor => fadeColor;

    /// <summary>플레이어가 이 스테이지에 진입할 때 발생. StageManager가 구독하여 전환 처리.</summary>
    public static event Action<Stage> OnPlayerEnteredStage;


    public Vector2 GetRespawnPoint(bool isCleared)
    {
        Transform point = isCleared ? afterClearSavePoint : beforeClearSavePoint;
        if (point != null) return point.position;

        Debug.LogWarning($"[Stage] {name}: savepoint 미설정");
        return transform.position;
    }

    private void OnDrawGizmosSelected()
    {
        if (beforeClearSavePoint != null)
        {
            Gizmos.color = Color.cyan;
            Gizmos.DrawSphere(beforeClearSavePoint.position, 0.3f);
        }
        if (afterClearSavePoint != null)
        {
            Gizmos.color = Color.green;
            Gizmos.DrawSphere(afterClearSavePoint.position, 0.3f);
        }
    }

    private void Awake()
    {
        // virtualCam에 ImpulseListener가 없으면 자동 추가
        if (virtualCam != null && impulseListener == null)
        {
            impulseListener = virtualCam.GetComponentInChildren<CinemachineImpulseListener>(true);
            if (impulseListener == null)
            {
                impulseListener = virtualCam.gameObject.AddComponent<CinemachineImpulseListener>();
                impulseListener.ApplyAfter          = CinemachineCore.Stage.Noise;
                impulseListener.ChannelMask         = 1;
                impulseListener.Gain                = 1f;
                impulseListener.Use2DDistance       = false;
                impulseListener.UseCameraSpace      = true;
                impulseListener.SignalCombinationMode = CinemachineImpulseListener.SignalCombinationModes.Additive;
            }
        }
        AutoFit();
    }

    public void Init(int index)
    {
        Index = index;

        if (index == 0)
            Activate();
    }

    private bool isFollowMode;

    public void Activate()
    {
        if (virtualCam == null)
        {
            Debug.LogWarning($"[Stage] Activate 실패 | {name} virtualCam null");
            return;
        }

        Player player = FindFirstObjectByType<Player>();
        if (player != null)
        {
            virtualCam.Follow = player.transform;
            player.controller.SetAbilities(allowedAbilities);
        }
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (other.CompareTag("Player") && !other.isTrigger)
            OnPlayerEnteredStage?.Invoke(this);
    }

    // SetActive(true) 시 이미 트리거 안에 있으면 Enter가 발생하지 않으므로 Stay로 보완
    private void OnTriggerStay2D(Collider2D other)
    {
        if (other.CompareTag("Player") && !other.isTrigger)
            OnPlayerEnteredStage?.Invoke(this);
    }

#if UNITY_EDITOR
    private void OnValidate()
    {
        // OnValidate 중엔 AddComponent/GameObject 생성 불가 → delayCall로 다음 틱에 실행
        UnityEditor.EditorApplication.delayCall += () =>
        {
            if (this != null) AutoFit();
        };
    }
#endif

    [ContextMenu("Auto Fit")]
    private void AutoFit()
    {
        Renderer[] renderers = GetComponentsInChildren<Renderer>();
        if (renderers.Length == 0)
        {
            Debug.LogWarning($"[Stage] {name}: 자식에 Renderer가 없어 자동 설정 불가");
            return;
        }

        Bounds bounds = GetRendererWorldBounds(renderers[0]);
        for (int i = 1; i < renderers.Length; i++)
            bounds.Encapsulate(GetRendererWorldBounds(renderers[i]));

        Vector2 localCenter = transform.InverseTransformPoint(bounds.center);

        // 플레이어 감지 트리거
        BoxCollider2D trigger = GetComponent<BoxCollider2D>();
        trigger.isTrigger = true;
        trigger.offset = new Vector2(localCenter.x, 0f);
        trigger.size = new Vector2(bounds.size.x + triggerPadding.x * 2f, bounds.size.y + triggerPadding.y * 2f);

        // 카메라 OrthographicSize: 맵 전체가 딱 들어오도록 조절
        if (virtualCam != null)
        {
            float aspect = Screen.width > 0 ? Screen.width / (float)Screen.height : 16f / 9f;
            float orthoByWidth  = (bounds.size.x + cameraHorizontalPadding * 2f) / (2f * aspect);
            float orthoByHeight = (bounds.size.y + cameraTopPadding + cameraBottomPadding) / 2f;
            float calculated = fitByHeight ? orthoByHeight : orthoByWidth;
            // 맵이 너무 크면 전체 고정 대신 플레이어 추적 모드 (maxFollowOrthoSize 크기로 따라감)
            isFollowMode = calculated > maxFollowOrthoSize;
            virtualCam.Lens.OrthographicSize = isFollowMode ? maxFollowOrthoSize : calculated;

            // follow 모드: body 컴포넌트가 없으면 추가 (없으면 Follow 타겟 설정해도 카메라가 안 움직임)
            if (isFollowMode)
            {
                if (!virtualCam.TryGetComponent<CinemachinePositionComposer>(out var composer))
                    composer = virtualCam.gameObject.AddComponent<CinemachinePositionComposer>();
                // 플레이어를 항상 화면 정중앙에 유지
                composer.Composition = new ScreenComposerSettings
                {
                    ScreenPosition = Vector2.zero,
                    DeadZone = new ScreenComposerSettings.DeadZoneSettings { Enabled = false },
                };

                // confiner 뷰포트 초과 허용: 꺼져 있으면 창이 confiner보다 클 때 카메라가 중앙에 고정됨
                if (virtualCam.TryGetComponent<CinemachineConfiner2D>(out var confiner))
                {
                    confiner.OversizeWindow = new CinemachineConfiner2D.OversizeWindowSettings
                    {
                        Enabled = true,
                        MaxWindowSize = maxFollowOrthoSize,
                    };
                }
            }
        }

        // 카메라 경계 콜라이더 (별도 자식) - 바닥 기준 = beforeClearSavePoint Y (없으면 렌더러 bounds 하단)
        BoxCollider2D camBounds = GetOrCreateCameraBounds();

        // before/after 세이브포인트 중 더 낮은 Y를 바닥 기준으로 사용 (없으면 렌더러 bounds 하단)
        float rendererBotLocal = localCenter.y - bounds.size.y * 0.5f;
        float beforeLocalY = beforeClearSavePoint != null
            ? transform.InverseTransformPoint(beforeClearSavePoint.position).y
            : float.MaxValue;
        float afterLocalY = afterClearSavePoint != null
            ? transform.InverseTransformPoint(afterClearSavePoint.position).y
            : float.MaxValue;
        float savepointLocalY = Mathf.Min(beforeLocalY, afterLocalY);
        if (savepointLocalY == float.MaxValue) savepointLocalY = rendererBotLocal;
        float stageBotLocal = Mathf.Min(savepointLocalY, rendererBotLocal);

        // 맵 상단 ~ 세이브포인트 간 높이로 계산 (세이브포인트가 기준 바닥)
        float mapTopLocal    = localCenter.y + bounds.size.y * 0.5f;
        float mapSpan        = mapTopLocal - stageBotLocal;
        float cameraViewHeight = (virtualCam != null) ? virtualCam.Lens.OrthographicSize * 2f : 0f;
        float baseHeight     = Mathf.Max(mapSpan, cameraViewHeight);
        float camHeight      = baseHeight + cameraTopPadding + cameraBottomPadding;

        float camCenterY = stageBotLocal - cameraBottomPadding + camHeight * 0.5f;

        camBounds.transform.localPosition = new Vector3(localCenter.x, camCenterY, 0f);
        camBounds.offset = Vector2.zero;
        camBounds.size = new Vector2(bounds.size.x + cameraHorizontalPadding * 2f, camHeight);
    }

    private BoxCollider2D GetOrCreateCameraBounds()
    {
        Transform child = transform.Find("_CameraBounds");
        if (child == null)
        {
            child = new GameObject("_CameraBounds").transform;
            child.SetParent(transform);
            child.localPosition = Vector3.zero;
        }

        if (!child.TryGetComponent(out BoxCollider2D col))
            col = child.gameObject.AddComponent<BoxCollider2D>();

        col.isTrigger = true;
        return col;
    }

    // 회전된 오브젝트도 정확히 계산: 로컬 꼭짓점을 world로 변환 후 AABB 생성
    private static Bounds GetRendererWorldBounds(Renderer renderer)
    {
        if (renderer is SpriteRenderer sr && sr.sprite != null)
        {
            Bounds local = sr.sprite.bounds;
            Vector3[] corners =
            {
                new(local.min.x, local.min.y, 0f),
                new(local.min.x, local.max.y, 0f),
                new(local.max.x, local.min.y, 0f),
                new(local.max.x, local.max.y, 0f),
            };

            Bounds result = new(renderer.transform.TransformPoint(corners[0]), Vector3.zero);
            for (int i = 1; i < corners.Length; i++)
                result.Encapsulate(renderer.transform.TransformPoint(corners[i]));
            return result;
        }

        // SpriteRenderer가 아니면 기존 방식 사용
        return renderer.bounds;
    }
}