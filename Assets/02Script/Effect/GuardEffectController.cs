using UnityEngine;

public class GuardEffectController : MonoBehaviour
{
    [Header("Reference")]
    [SerializeField] private ParticleSystem innerGuard;

    [Header("Rotate")]
    [SerializeField] private float _rotateSpeedY = 180f;
    [SerializeField] private bool _useUnscaledTime = false;

    private float _currentY;

    private void Update()
    {
        if (innerGuard == null)
            return;

        if (!innerGuard.isPlaying)
            return;

        float deltaTime = _useUnscaledTime ? Time.unscaledDeltaTime : Time.deltaTime;

        _currentY = Mathf.Repeat(_currentY + (_rotateSpeedY * deltaTime), 360f);
        innerGuard.transform.localRotation = Quaternion.Euler(0f, _currentY, 0f);
    }

    public void Play()
    {
        if (innerGuard == null)
            return;

        innerGuard.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);

        _currentY = 0f;
        innerGuard.transform.localRotation = Quaternion.identity;

        innerGuard.Play(true);
    }

    public void Stop()
    {
        if (innerGuard != null)
            innerGuard.Stop(true, ParticleSystemStopBehavior.StopEmitting);
    }

    public void StopAndDestroy(float delay = 0f)
    {
        Stop();
        Destroy(gameObject, delay);
    }
}