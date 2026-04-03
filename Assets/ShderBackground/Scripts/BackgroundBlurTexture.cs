using UnityEngine;

public class BackgroundBlurTexture : MonoBehaviour
{

    [SerializeField] private Texture sourceTexture;
    [SerializeField] private Material horizontalMat;
    [SerializeField] private Material verticalMat;
    [SerializeField] private SpriteRenderer targetRenderer;

    [SerializeField] private int textureWidth = 512;
    [SerializeField] private int textureHeight = 512;
    [SerializeField] private float blurRadius = 5f;
    [SerializeField] private float sigma = 2f;

    private RenderTexture tempRT;
    private RenderTexture finalRT;

    private void Start()
    {
        if (sourceTexture == null || horizontalMat == null || verticalMat == null || targetRenderer == null)
        {
            Debug.LogError("BlurTexture: 필요한 참조가 비어 있습니다.");
            return;
        }

        GaussianBlur();
    }

    void GaussianBlur()
    {
        tempRT = new RenderTexture(textureWidth, textureHeight, 0, RenderTextureFormat.ARGB32);
        finalRT = new RenderTexture(textureWidth, textureHeight, 0, RenderTextureFormat.ARGB32);

        tempRT.Create();
        finalRT.Create();

        horizontalMat.SetFloat("_BlurRadius", blurRadius);
        horizontalMat.SetFloat("_Sigma", sigma);

        verticalMat.SetFloat("_BlurRadius", blurRadius);
        verticalMat.SetFloat("_Sigma", sigma);

        Graphics.Blit(sourceTexture, tempRT, horizontalMat);
        Graphics.Blit(tempRT, finalRT, verticalMat);

        targetRenderer.sharedMaterial.SetTexture("_MainTexture", finalRT);
    }

    private void OnValidate()
    {
        GaussianBlur();
    }

    private void OnDestroy()
    {
        if (tempRT != null)
        {
            tempRT.Release();
            tempRT = null;
        }

        if (finalRT != null)
        {
            finalRT.Release();
            finalRT = null;
        }
    }
}
