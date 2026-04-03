using UnityEngine;
using Unity.Cinemachine;

public class CameraManager : MonoBehaviour
{
    public static CameraManager Instance { get; private set; }


    [SerializeField, Tooltip("라이브 카메라 우선순위")] private int livePriority = 100;
    [SerializeField, Tooltip("비활성 카메라 순위")] private int idlePriority = 0;


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
    }

    public void SwitchTo(CinemachineCamera nextCam)
    {
        // 동일한 카메라일 경우 동작 안함
        if (nextCam == null || nextCam == currentCamera) return;
            
        // 현재 카메라 비활성화
        if (currentCamera != null) currentCamera.Priority.Value = idlePriority;

        // 다음 카메라 활성화
        nextCam.Priority.Value = livePriority;
        currentCamera = nextCam;
        Debug.Log($"[Camera Manager] Camera Transition: {currentCamera.gameObject.name} => {nextCam.gameObject.name}");
    }
}
