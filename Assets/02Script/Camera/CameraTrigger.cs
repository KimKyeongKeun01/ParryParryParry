using UnityEngine;
using static CameraManager;

public class CameraTrigger : MonoBehaviour
{
    private enum CameraGroup
    {
        Default,
        Extra
    }

    [SerializeField] private CameraGroup group = CameraGroup.Default;
    [SerializeField] private DefaultCameraType defaultCameraType = DefaultCameraType.Normal_12;
    [SerializeField] private ExtraCameraType extraCameraType = ExtraCameraType.None;


    private void OnTriggerEnter2D(Collider2D collision)
    {
        if (!collision.CompareTag("Player")) return;

        if (group == CameraGroup.Default) Instance?.SwitchTo(defaultCameraType);
        else Instance?.SwitchTo(extraCameraType);
    }
}
