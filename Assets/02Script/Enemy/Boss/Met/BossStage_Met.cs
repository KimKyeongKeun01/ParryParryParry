using UnityEngine;

public class BossStage_Met : BossStage
{
    [SerializeField] private CameraShakeProfile growlingProfile;


    public void S_GrowlingShake()
    {
        CameraManager.Instance.Shake.Play(growlingProfile);
    }
}
