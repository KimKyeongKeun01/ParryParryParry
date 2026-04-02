using UnityEngine;

public class GuardEffectController : MonoBehaviour
{
    [SerializeField] private ParticleSystem innerGuard;
    [SerializeField] private ParticleSystem outerGuard;

    public void Play()
    {
        if (innerGuard != null)
        {
            innerGuard.Play(true);
        }

        if (outerGuard != null)
        {
            outerGuard.Play(true);
        }
    }

    public void Stop()
    {
        if(innerGuard != null) 
            innerGuard.Stop(true, ParticleSystemStopBehavior.StopEmitting);

        if(outerGuard != null)
            outerGuard.Stop(true, ParticleSystemStopBehavior.StopEmitting);
    }

    public void StopAndDestroy(float delay = 0f)
    {
        Stop();
        Destroy(gameObject, delay);
    }
}
