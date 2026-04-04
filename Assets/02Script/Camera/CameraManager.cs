using UnityEngine;
using Unity.Cinemachine;

public class CameraManager : MonoBehaviour
{
    public static CameraManager Instance { get; private set; }

    // 일반 스테이지용 카메라
    public enum DefaultCameraType
    {
        Close_10,
        Normal_12,
        Wide_15,
        VeryWide_20,
    }

    // 특수 상황용 카메라
    public enum ExtraCameraType // 필요한 만큼 정의
    {
        None,
        Boss_Met,
        Boss_Utan,
    }

    [Header("Priority")]
    [SerializeField, Tooltip("라이브 카메라 우선순위")] private int livePriority = 100;
    [SerializeField, Tooltip("비활성 카메라 순위")] private int idlePriority = 0;


    [Header("Cameras")]
    [Tooltip("enum DefaultCameraType 순서와 배열 순서를 반드시 동일하게 맞출 것")]
    [SerializeField] private CinemachineCamera[] defaultCameras;
    [Tooltip("enum ExtraCameraType 순서와 배열 순서를 반드시 동일하게 맞출 것 (0번 None은 비워둬도 됨)")]
    [SerializeField] private CinemachineCamera[] extraCameras;


    #region 참조
    public CameraShakeController Shake { get; private set; }
    private CinemachineCamera currentCamera;
    #endregion

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        Shake = GetComponent<CameraShakeController>();

        InitCamera(DefaultCameraType.Normal_12);
    }

    #region Core
    /// <summary> 실제 카메라 전환 처리. </summary>
    public void SwitchTo(CinemachineCamera nextCam)
    {
        // 동일한 카메라일 경우 동작 안함
        if (nextCam == null || nextCam == currentCamera) return;
        string prevName = currentCamera != null ? currentCamera.gameObject.name : "None";

        // 현재 카메라 비활성화
        if (currentCamera != null) currentCamera.Priority.Value = idlePriority;

        // 다음 카메라 활성화
        nextCam.Priority.Value = livePriority;
        currentCamera = nextCam;

        Debug.Log($"[CameraManager] Camera Transition: {prevName} => {nextCam.gameObject.name}");
    }

    /// <summary> 시작 카메라 강제 조정 </summary>
    public void InitCamera(DefaultCameraType type)
    {
        int index = (int)type;

        if (!IsValidIndex(defaultCameras, index))
        {
            Debug.LogWarning($"[CameraManager] Initial Default Camera index out of range: {type} ({index})");
            return;
        }

        // 모든 기본 카메라 우선순위 초기화
        for (int i = 0; i < defaultCameras.Length; i++)
        {
            if (defaultCameras[i] != null)
                defaultCameras[i].Priority.Value = idlePriority;
        }

        // 모든 특수 카메라 우선순위 초기화
        for (int i = 0; i < extraCameras.Length; i++)
        {
            if (extraCameras[i] != null)
                extraCameras[i].Priority.Value = idlePriority;
        }

        // 카메라 세팅
        defaultCameras[index].Priority.Value = livePriority;
        currentCamera = defaultCameras[index];
    }
    #endregion

    #region Camera Switch
    /// <summary> Default 카메라 전환 처리. </summary>
    public void SwitchTo(DefaultCameraType type)
    {
        int index = (int)type;

        if (!IsValidIndex(defaultCameras, index)) return;

        SwitchTo(defaultCameras[index]);
    }

    /// <summary> Extra 카메라 전환 처리. </summary>
    public void SwitchTo(ExtraCameraType type)
    {
        if (type == ExtraCameraType.None) return;

        int index = (int)type;

        // None 제외
        SwitchTo(extraCameras[index - 1]);
    }
    #endregion

    #region Internal
    /// <summary> 유효 카메라 배열 체크 </summary>
    private bool IsValidIndex(CinemachineCamera[] cameraArray, int index)
    {
        return cameraArray != null
            && index >= 0
            && index < cameraArray.Length;
    }
    #endregion
}
