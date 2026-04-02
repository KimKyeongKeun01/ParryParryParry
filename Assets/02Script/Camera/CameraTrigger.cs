using Unity.Cinemachine;
using UnityEngine;

public class CameraTrigger : MonoBehaviour
{
    [SerializeField] private CinemachineCamera _zoneCamera;

    private void OnTriggerEnter2D(Collider2D collision)
    {
        if (!collision.CompareTag("Player")) return;

        CameraManager.Instance?.SwitchTo(_zoneCamera);
    }
}
