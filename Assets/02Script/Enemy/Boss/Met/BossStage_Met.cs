using Unity.VisualScripting;
using UnityEngine;

public class BossStage_Met : BossStage
{
    [SerializeField] private CameraShakeProfile stompProfile;
    [SerializeField] private CameraShakeProfile brokeProfile;


    public void S_Stomp1Shake()
    {
        CameraManager.Instance.Shake.Play(stompProfile, 0.5f);
    }

    public void S_Stomp2Shake()
    {
        CameraManager.Instance.Shake.Play(stompProfile, 1f);
    }

    public void S_Stomp3Shake()
    {
        CameraManager.Instance.Shake.Play(stompProfile, 1.5f);
    }

    public void S_Broke3Shake()
    {
        CameraManager.Instance.Shake.Play(brokeProfile, Vector2.left);
    }
}
