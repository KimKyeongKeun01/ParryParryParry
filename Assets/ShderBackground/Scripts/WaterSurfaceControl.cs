using UnityEngine;

public class WaterSurfaceControl : MonoBehaviour
{
    [SerializeField] private Renderer waterRenderer;
    [SerializeField] private Material targetMaterial;

    [Header("EnterWaveSetting")]
    [SerializeField] private float EnterImpactX;
    [SerializeField] private float EnterImpactStrength;
    [SerializeField] private float EnterImpactWaveSpeed;
    [SerializeField] private float EnterImpactWaveFrequency;
    [SerializeField] private float EnterImpactWaveDecay;
    [SerializeField] private float EnterImpactRadius;

    private int nextIndex = 0;

    public void EnterWave()
    {

        targetMaterial.SetFloat("_ImpactX", EnterImpactX);
        targetMaterial.SetFloat("_ImpactTime", Time.time);
        targetMaterial.SetFloat("_ImpactStrength", EnterImpactStrength);
        targetMaterial.SetFloat("_ImpactWaveSpeed", EnterImpactWaveSpeed);
        targetMaterial.SetFloat("_ImpactWaveFrequency", EnterImpactWaveFrequency);
        targetMaterial.SetFloat("_ImpactWaveDecay", EnterImpactWaveDecay);
        targetMaterial.SetFloat("_ImpactWaveRadius", EnterImpactRadius);
    }

    private void OnTriggerEnter2D(Collider2D collision)
    {
        Player player = collision.GetComponent<Player>();
        if (player != null)
        {
            EnterWave();
        }
    }
}
