using UnityEngine;

public class PerfectGuardEffectController : MonoBehaviour
{
    [SerializeField] private ParticleSystem layer1;
    //[SerializeField] private ParticleSystem layer2;

    public void Play()
    {
        if (layer1 != null)
            layer1.Play();

        //if (layer2 != null)
        //    layer2.Play();
    }

    public float GetLifetime()
    {
        float maxLifetime = 0f;

        if (layer1 != null)
            maxLifetime = Mathf.Max(maxLifetime, GetParticleLifetime(layer1));

        //if (layer2 != null)
        //    maxLifetime = Mathf.Max(maxLifetime, GetParticleLifetime(layer2));

        return maxLifetime;
    }

    private float GetParticleLifetime(ParticleSystem ps)
    {
        var main = ps.main;
        float lifetime = main.duration;

        if (!main.loop)
        {
            lifetime += main.startLifetime.constantMax;
        }

        return lifetime;
    }
}
