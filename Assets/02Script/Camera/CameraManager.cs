using UnityEngine;
using Unity.Cinemachine;

public class CameraManager : MonoBehaviour
{
    public static CameraManager Instance { get; private set; }

    [SerializeField] private int livePriority = 100;
    [SerializeField] private int idlePriority = 0;
    private CinemachineCamera currentCamera;


    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
    }

    public void SwitchTo(CinemachineCamera nextCam)
    {
        if (nextCam == null || nextCam == currentCamera)
        {
            Debug.Log($"[Camera Manager] Already This Camera: {currentCamera.gameObject.name}");
            return;
        }
            
        if (currentCamera != null) currentCamera.Priority.Value = idlePriority;

        nextCam.Priority.Value = livePriority;
        currentCamera = nextCam;
        Debug.Log($"[Camera Manager] Camera Transition: {currentCamera.gameObject.name} => {nextCam.gameObject.name}");
    }
}
