using UnityEngine;

public class WaterSurfaceControl : MonoBehaviour
{
    [SerializeField] private Renderer waterRenderer;
    [SerializeField] private Material targetMaterial;

    [Header("EnterWaveSetting")]
    [SerializeField] private Transform feetPos;
    [SerializeField] private float enterImpactStrength;
    [SerializeField] private float enterImpactWaveSpeed;
    [SerializeField] private float enterImpactWaveFrequency;
    [SerializeField] private float enterImpactWaveDecay;
    [SerializeField] private float enterImpactRadius;

    private int nextIndex = 0;

    public void EnterWave()
    {

        targetMaterial.SetFloat("_ImpactX", feetPos.position.x);
        targetMaterial.SetFloat("_ImpactTime", Time.time);
        targetMaterial.SetFloat("_ImpactStrength", enterImpactStrength);
        targetMaterial.SetFloat("_ImpactWaveSpeed", enterImpactWaveSpeed);
        targetMaterial.SetFloat("_ImpactWaveFrequency", enterImpactWaveFrequency);
        targetMaterial.SetFloat("_ImpactWaveDecay", enterImpactWaveDecay);
        targetMaterial.SetFloat("_ImpactWaveRadius", enterImpactRadius);
    }

    private void OnTriggerEnter2D(Collider2D collision)
    {
        Player player = collision.GetComponent<Player>();
        if (player != null)
        {
            Debug.Log("플레이어 들어옴");
            EnterWave();
        }
    }
}
