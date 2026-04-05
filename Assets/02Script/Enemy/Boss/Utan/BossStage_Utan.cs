using UnityEngine;

public class BossStage_Utan : BossStage
{
    [SerializeField] private CameraShakeProfile landingProfile;
    [SerializeField] private CameraShakeProfile cryingProfile;

    private void Update()
    {
        // 테스트
        if (Input.GetKeyDown(KeyCode.H))
        {
            S_CryingShake();
        }
        else if (Input.GetKeyDown(KeyCode.J))
        {
            S_LandingShake();
        }
    }

    public void S_LandingShake()
    {
        Debug.Log("[Boss Stage] Landing Shake");
        Debug.Log($"[Signal Shake] obj={name}, id={GetInstanceID()}, frame={Time.frameCount}");
        Debug.Log($"[Signal Shake] mainCam={Camera.main?.name}");

        CameraManager.Instance.Shake.Play(landingProfile, 3);
    }

    public void S_CryingShake()
    {
        Debug.Log("[Boss Stage] Crying Shake");
        Debug.Log($"[Signal Shake] obj={name}, id={GetInstanceID()}, frame={Time.frameCount}");
        Debug.Log($"[Signal Shake] mainCam={Camera.main?.name}");

        CameraManager.Instance.Shake.Play(cryingProfile, Vector2.left);
    }
}
