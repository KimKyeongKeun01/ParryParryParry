using UnityEngine;
using UnityEngine.Tilemaps;
using static UnityEditor.PlayerSettings;

public class TileNeighborData : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private Tilemap tilemap;
    [SerializeField] private Material targetMaterial;
    [SerializeField] private Transform feetPoint;

    [Header("Static Tile Data Shader Property Names")]
    [SerializeField] private string tileDataTexProperty = "_TileDataTex";
    [SerializeField] private string tilemapOriginProperty = "_TilemapOrigin";
    [SerializeField] private string tilemapSizeProperty = "_TilemapSize";
    [SerializeField] private string cellSizeProperty = "_CellSize";

    private Texture2D dataTexture;      // 기존 상하좌우 정보

    private BoundsInt cachedBounds;
    private int width;
    private int height;


    [Header("Shader Properties")]
    [SerializeField] private string stepHeatTexProperty = "_StepHeatTex";
    [SerializeField] private string heatOriginProperty = "_HeatWorldOrigin";
    [SerializeField] private string heatSizeProperty = "_HeatWorldSize";
    [SerializeField] private string changeColorProperty = "_ChangeColor";

    [Header("Visual")]
    [SerializeField] private Color changeColor = Color.red;
    [SerializeField] private float fadeSpeed = 1.5f;

    [Header("Footprint")]
    [SerializeField] private float footprintWidth = 0.4f;
    [SerializeField] private float footprintHeight = 0.1f;
    [SerializeField] private int pixelsPerUnit = 32;

    private Texture2D stepHeatTexture;
    private float[] heatValues;
    private Color[] heatPixels;

    private BoundsInt cellBounds;
    private Vector3 worldOrigin;
    private Vector2 worldSize;

    private int texWidth;
    private int texHeight;

    private void Start()
    {
        InitializeHeatTexture();
        Bake();
    }

    private void InitializeHeatTexture()
    {
        tilemap.CompressBounds();
        cellBounds = tilemap.cellBounds;

        Vector3 minWorld = tilemap.CellToWorld(new Vector3Int(cellBounds.xMin, cellBounds.yMin, 0));
        Vector3 maxWorld = tilemap.CellToWorld(new Vector3Int(cellBounds.xMax, cellBounds.yMax, 0));

        worldOrigin = minWorld;
        worldSize = new Vector2(maxWorld.x - minWorld.x, maxWorld.y - minWorld.y);

        texWidth = Mathf.CeilToInt(worldSize.x * pixelsPerUnit);
        texHeight = Mathf.CeilToInt(worldSize.y * pixelsPerUnit);

        stepHeatTexture = new Texture2D(texWidth, texHeight, TextureFormat.RGBA32, false, true);
        stepHeatTexture.filterMode = FilterMode.Point;
        stepHeatTexture.wrapMode = TextureWrapMode.Clamp;
        stepHeatTexture.name = "StepHeat_WorldSpace";

        heatValues = new float[texWidth * texHeight];
        heatPixels = new Color[texWidth * texHeight];

        for (int i = 0; i < heatPixels.Length; i++)
        {
            heatValues[i] = 0f;
            heatPixels[i] = Color.black;
        }

        stepHeatTexture.SetPixels(heatPixels);
        stepHeatTexture.Apply(false, false);

        targetMaterial.SetTexture(stepHeatTexProperty, stepHeatTexture);
        targetMaterial.SetVector(heatOriginProperty, new Vector4(worldOrigin.x, worldOrigin.y, 0, 0));
        targetMaterial.SetVector(heatSizeProperty, new Vector4(worldSize.x, worldSize.y, 0, 0));
        targetMaterial.SetColor(changeColorProperty, changeColor);
    }

    private void Update()
    {
        if (stepHeatTexture == null) return;

        FadeHeat();
        StampFootprint();
        UploadHeatTexture();

        targetMaterial.SetColor(changeColorProperty, changeColor);
    }

    private void FadeHeat()
    {
        float delta = fadeSpeed * Time.deltaTime;

        for (int i = 0; i < heatValues.Length; i++)
        {
            heatValues[i] = Mathf.Max(0f, heatValues[i] - delta);
        }
    }

    private void StampFootprint()
    {
        if (feetPoint == null) return;

        float minX = feetPoint.position.x - footprintWidth * 0.5f;
        float maxX = feetPoint.position.x + footprintWidth * 0.5f;
        float minY = feetPoint.position.y - footprintHeight * 0.5f;
        float maxY = feetPoint.position.y + footprintHeight * 0.5f;

        int pxMin = WorldToPixelX(minX);
        int pxMax = WorldToPixelX(maxX);
        int pyMin = WorldToPixelY(minY);
        int pyMax = WorldToPixelY(maxY);

        pxMin = Mathf.Clamp(pxMin, 0, texWidth - 1);
        pxMax = Mathf.Clamp(pxMax, 0, texWidth - 1);
        pyMin = Mathf.Clamp(pyMin, 0, texHeight - 1);
        pyMax = Mathf.Clamp(pyMax, 0, texHeight - 1);

        for (int y = pyMin; y <= pyMax; y++)
        {
            for (int x = pxMin; x <= pxMax; x++)
            {
                int index = y * texWidth + x;
                heatValues[index] = 1f;
            }
        }
    }

    private int WorldToPixelX(float worldX)
    {
        float normalized = (worldX - worldOrigin.x) / worldSize.x;
        return Mathf.FloorToInt(normalized * texWidth);
    }

    private int WorldToPixelY(float worldY)
    {
        float normalized = (worldY - worldOrigin.y) / worldSize.y;
        return Mathf.FloorToInt(normalized * texHeight);
    }

    private void UploadHeatTexture()
    {
        for (int i = 0; i < heatValues.Length; i++)
        {
            float v = heatValues[i];
            heatPixels[i] = new Color(v, v, v, 1f);
        }

        stepHeatTexture.SetPixels(heatPixels);
        stepHeatTexture.Apply(false, false);
    }

#if UNITY_EDITOR
    private void OnValidate()
    {
        if (!Application.isPlaying)
        {
            if (tilemap != null && targetMaterial != null)
            {
                Bake();
            }
        }
    }
#endif
    public void Bake()
    {
        if (tilemap == null || targetMaterial == null)
        {
            Debug.LogWarning("Tilemap or Material is missing.");
            return;
        }

        tilemap.CompressBounds();
        cachedBounds = tilemap.cellBounds;

        width = cachedBounds.size.x;
        height = cachedBounds.size.y;

        if (width <= 0 || height <= 0)
        {
            Debug.LogWarning("Tilemap bounds are empty.");
            return;
        }

        BakeStaticTileData();
        ApplyCommonMaterialProperties();
    }

    private void BakeStaticTileData()
    {
        if (dataTexture == null || dataTexture.width != width || dataTexture.height != height)
        {
            dataTexture = new Texture2D(width, height, TextureFormat.RGBA32, false, true);
            dataTexture.filterMode = FilterMode.Point;
            dataTexture.wrapMode = TextureWrapMode.Clamp;
            dataTexture.name = "Tilemap_Data_Texture";
        }

        for (int y = 0; y < height; y++)
        {
            for (int x = 0; x < width; x++)
            {
                Vector3Int pos = new Vector3Int(cachedBounds.xMin + x, cachedBounds.yMin + y, 0);

                if (!tilemap.HasTile(pos))
                {
                    dataTexture.SetPixel(x, y, Color.clear);
                    continue;
                }

                bool up = tilemap.HasTile(pos + Vector3Int.up);
                bool right = tilemap.HasTile(pos + Vector3Int.right);
                bool down = tilemap.HasTile(pos + Vector3Int.down);
                bool left = tilemap.HasTile(pos + Vector3Int.left);

                Color data = new Color(
                    up ? 0f : 1f,
                    right ? 0f : 1f,
                    down ? 0f : 1f,
                    left ? 0f : 1f
                );

                dataTexture.SetPixel(x, y, data);
            }
        }

        dataTexture.Apply(false, false);
        targetMaterial.SetTexture(tileDataTexProperty, dataTexture);
    }


    private void ApplyCommonMaterialProperties()
    {
        Vector3 cellSize = tilemap.cellSize;
        Vector3 originWorld = tilemap.CellToWorld(new Vector3Int(cachedBounds.xMin, cachedBounds.yMin, 0));

        targetMaterial.SetVector(tilemapOriginProperty, new Vector2(originWorld.x, originWorld.y));
        targetMaterial.SetVector(tilemapSizeProperty, new Vector2(width, height));
        targetMaterial.SetVector(cellSizeProperty, new Vector2(cellSize.x, cellSize.y));
        targetMaterial.SetColor(changeColorProperty, changeColor);
    }


}
