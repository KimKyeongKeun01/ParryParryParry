using UnityEngine;

public class FullscreenShockwaveController : MonoBehaviour
{
    [Header("Reference")]
    [SerializeField] private Material _fullscreenMaterial;
    [SerializeField] private Camera _targetCamera;

    [Header("Shockwave")]
    [SerializeField] private float _duration = 0.35f;
    [SerializeField] private float _radius = 0.35f;
    [SerializeField] private float _ringWidth = 0.018f;
    [SerializeField] private float _strength = 0.012f;

    [Header("Debug")]
    [SerializeField] private bool _playOnStart = false;
    [SerializeField] private Transform _debugWorldTarget;

    private const int SlotCount = 4;

    private readonly ShockwaveSlot[] _slots = new ShockwaveSlot[SlotCount];
    private int _nextSlotIndex;

    private static readonly int[] ShockProgressIds =
    {
        Shader.PropertyToID("_ShockProgress"),
        Shader.PropertyToID("_ShockProgress_1"),
        Shader.PropertyToID("_ShockProgress_2"),
        Shader.PropertyToID("_ShockProgress_3")
    };

    private static readonly int[] HitCenterUVIds =
    {
        Shader.PropertyToID("_HitCenterUV"),
        Shader.PropertyToID("_HitCenterUV_1"),
        Shader.PropertyToID("_HitCenterUV_2"),
        Shader.PropertyToID("_HitCenterUV_3")
    };

    private static readonly int[] RadiusIds =
    {
        Shader.PropertyToID("_Radius"),
        Shader.PropertyToID("_Radius_1"),
        Shader.PropertyToID("_Radius_2"),
        Shader.PropertyToID("_Radius_3")
    };

    private static readonly int[] RingWidthIds =
    {
        Shader.PropertyToID("_RingWidth"),
        Shader.PropertyToID("_RingWidth_1"),
        Shader.PropertyToID("_RingWidth_2"),
        Shader.PropertyToID("_RingWidth_3")
    };

    private static readonly int[] StrengthIds =
    {
        Shader.PropertyToID("_Strength"),
        Shader.PropertyToID("_Strength_1"),
        Shader.PropertyToID("_Strength_2"),
        Shader.PropertyToID("_Strength_3")
    };

    private struct ShockwaveSlot
    {
        public bool IsPlaying;
        public Vector2 ViewportUV;
        public float Elapsed;
    }

    private void Awake()
    {
        if (_targetCamera == null)
            _targetCamera = Camera.main;

        InitializeSlots();
        ApplyStaticPropertiesToAllSlots();
        HideAllEffectsImmediate();
    }

    private void Start()
    {
        if (_playOnStart == false)
            return;

        if (_debugWorldTarget != null)
        {
            PlayAtWorld(_debugWorldTarget.position);
            return;
        }

        PlayAtViewportUV(new Vector2(0.5f, 0.5f));
    }

    private void Update()
    {
        for (int i = 0; i < SlotCount; i++)
        {
            if (_slots[i].IsPlaying == false)
                continue;

            _slots[i].Elapsed += Time.deltaTime;

            float normalizedTime = Mathf.Clamp01(_slots[i].Elapsed / _duration);
            float easedTime = 1f - Mathf.Pow(1f - normalizedTime, 3f);

            float progress = Mathf.Lerp(0f, _radius, easedTime);
            float currentStrength = Mathf.Lerp(_strength, 0f, normalizedTime);

            _fullscreenMaterial.SetFloat(ShockProgressIds[i], progress);
            _fullscreenMaterial.SetFloat(StrengthIds[i], currentStrength);

            if (normalizedTime >= 1f)
                StopSlot(i);
        }
    }

    public void PlayAtWorld(Vector3 worldPosition)
    {
        if (_targetCamera == null)
            _targetCamera = Camera.main;

        if (_targetCamera == null)
        {
            Debug.LogWarning("[FullscreenShockwaveController] PlayAtWorld 실패 - target camera가 없습니다.");
            return;
        }

        Vector3 screenPoint = _targetCamera.WorldToScreenPoint(worldPosition);
        if (screenPoint.z <= 0f)
            return;

        Vector2 viewportUV = new Vector2(
            screenPoint.x / Screen.width,
            screenPoint.y / Screen.height
        );

        PlayAtViewportUV(viewportUV);
    }

    public void PlayAtWorld(Vector2 worldPosition)
    {
        PlayAtWorld((Vector3)worldPosition);
    }

    public void PlayAtScreenPixel(Vector2 screenPixel)
    {
        Vector2 viewportUV = new Vector2(
            screenPixel.x / Screen.width,
            screenPixel.y / Screen.height
        );

        PlayAtViewportUV(viewportUV);
    }

    public void PlayAtViewportUV(Vector2 viewportUV)
    {
        int slotIndex = AllocateSlot();

        _slots[slotIndex].IsPlaying = true;
        _slots[slotIndex].ViewportUV = viewportUV;
        _slots[slotIndex].Elapsed = 0f;

        ApplyStaticPropertiesToSlot(slotIndex);
        _fullscreenMaterial.SetVector(HitCenterUVIds[slotIndex], viewportUV);
        _fullscreenMaterial.SetFloat(ShockProgressIds[slotIndex], 0f);
        _fullscreenMaterial.SetFloat(StrengthIds[slotIndex], _strength);
    }

    public void StopNow()
    {
        for (int i = 0; i < SlotCount; i++)
            StopSlot(i);
    }

    private int AllocateSlot()
    {
        for (int i = 0; i < SlotCount; i++)
        {
            int checkIndex = (_nextSlotIndex + i) % SlotCount;
            if (_slots[checkIndex].IsPlaying == false)
            {
                _nextSlotIndex = (checkIndex + 1) % SlotCount;
                return checkIndex;
            }
        }

        int overwriteIndex = _nextSlotIndex;
        _nextSlotIndex = (_nextSlotIndex + 1) % SlotCount;
        return overwriteIndex;
    }

    private void StopSlot(int slotIndex)
    {
        _slots[slotIndex].IsPlaying = false;
        _slots[slotIndex].ViewportUV = Vector2.zero;
        _slots[slotIndex].Elapsed = 0f;

        _fullscreenMaterial.SetVector(HitCenterUVIds[slotIndex], Vector2.zero);
        _fullscreenMaterial.SetFloat(ShockProgressIds[slotIndex], -10f);
        _fullscreenMaterial.SetFloat(StrengthIds[slotIndex], 0f);
    }

    private void InitializeSlots()
    {
        for (int i = 0; i < SlotCount; i++)
        {
            _slots[i].IsPlaying = false;
            _slots[i].ViewportUV = Vector2.zero;
            _slots[i].Elapsed = 0f;
        }
    }

    private void ApplyStaticPropertiesToAllSlots()
    {
        for (int i = 0; i < SlotCount; i++)
            ApplyStaticPropertiesToSlot(i);
    }

    private void ApplyStaticPropertiesToSlot(int slotIndex)
    {
        _fullscreenMaterial.SetFloat(RadiusIds[slotIndex], _radius);
        _fullscreenMaterial.SetFloat(RingWidthIds[slotIndex], _ringWidth);
        _fullscreenMaterial.SetFloat(StrengthIds[slotIndex], 0f);
    }

    private void HideAllEffectsImmediate()
    {
        for (int i = 0; i < SlotCount; i++)
        {
            _fullscreenMaterial.SetVector(HitCenterUVIds[i], Vector2.zero);
            _fullscreenMaterial.SetFloat(ShockProgressIds[i], -10f);
            _fullscreenMaterial.SetFloat(StrengthIds[i], 0f);
        }
    }
}