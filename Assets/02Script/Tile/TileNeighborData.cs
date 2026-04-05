using System.Collections.Generic;
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

    [Header("Step Heat Shader Property Names")]
    [SerializeField] private string stepHeatTexProperty = "_StepHeatTex";
    [SerializeField] private string changeColorProperty = "_ChangeColor";

    [Header("Step Heat Settings")]
    [SerializeField] private Color changeColor = Color.cyan;
    [SerializeField] private float fadeSpeed = 1.5f;

    [Header("Heat Texture Limits")]
    [SerializeField] private int maxHeatTextureWidth = 512;
    [SerializeField] private int maxHeatTextureHeight = 512;

    private Texture2D dataTexture;      // 기존 상하좌우 정보
    private Texture2D stepHeatTexture;  // 밟힘 강도 정보

    private float[] stepHeatValues;
    private Color[] stepHeatPixels;

    private BoundsInt cachedBounds;
    private int width;
    private int height;

    // active pixel optimization
    private readonly List<int> activeIndices = new List<int>();
    private int[] activeIndexLookup; // -1: 비활성, 그 외: activeIndices 내 위치

    // dirty pixel optimization
    private readonly List<int> dirtyIndices = new List<int>();
    private bool[] dirtyLookup;

    private Color _lastAppliedColor = Color.clear;

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
        InitializeStepHeatTexture();
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

    private void InitializeStepHeatTexture()
    {
        // TODO 해결:
        // 원래 width * height 그대로 잡지 않고 제한된 해상도로 사용
        int texWidth = Mathf.Min(width, maxHeatTextureWidth);
        int texHeight = Mathf.Min(height, maxHeatTextureHeight);

        bool needCreate =
            stepHeatTexture == null ||
            stepHeatTexture.width != texWidth ||
            stepHeatTexture.height != texHeight ||
            stepHeatValues == null ||
            stepHeatValues.Length != texWidth * texHeight ||
            stepHeatPixels == null ||
            stepHeatPixels.Length != texWidth * texHeight ||
            activeIndexLookup == null ||
            activeIndexLookup.Length != texWidth * texHeight ||
            dirtyLookup == null ||
            dirtyLookup.Length != texWidth * texHeight;

        if (!needCreate)
            return;

        stepHeatTexture = new Texture2D(texWidth, texHeight, TextureFormat.RGBA32, false, true);
        stepHeatTexture.filterMode = FilterMode.Point;
        stepHeatTexture.wrapMode = TextureWrapMode.Clamp;
        stepHeatTexture.name = "Tilemap_StepHeat_Texture";

        int pixelCount = texWidth * texHeight;

        stepHeatValues = new float[pixelCount];
        stepHeatPixels = new Color[pixelCount];
        activeIndexLookup = new int[pixelCount];
        dirtyLookup = new bool[pixelCount];

        activeIndices.Clear();
        dirtyIndices.Clear();

        for (int i = 0; i < pixelCount; i++)
        {
            stepHeatValues[i] = 0f;
            stepHeatPixels[i] = Color.black;
            activeIndexLookup[i] = -1;
            dirtyLookup[i] = false;
        }

        stepHeatTexture.SetPixels(stepHeatPixels);
        stepHeatTexture.Apply(false, false);

        targetMaterial.SetTexture(stepHeatTexProperty, stepHeatTexture);
    }

    private void ApplyCommonMaterialProperties()
    {
        Vector3 cellSize = tilemap.cellSize;
        Vector3 originWorld = tilemap.CellToWorld(new Vector3Int(cachedBounds.xMin, cachedBounds.yMin, 0));

        targetMaterial.SetVector(tilemapOriginProperty, new Vector2(originWorld.x, originWorld.y));
        targetMaterial.SetVector(tilemapSizeProperty, new Vector2(width, height));
        targetMaterial.SetVector(cellSizeProperty, new Vector2(cellSize.x, cellSize.y));

        if (_lastAppliedColor != changeColor)
        {
            targetMaterial.SetColor(changeColorProperty, changeColor);
            _lastAppliedColor = changeColor;
        }
    }

    private void Update()
    {
        
        if (tilemap == null || targetMaterial == null || feetPoint == null)
            return;

        if (stepHeatTexture == null || stepHeatValues == null || stepHeatPixels == null)
            return;

        FadeStepHeatActiveOnly();
        StampFootstep();
        UploadDirtyPixelsOnly();

        if (_lastAppliedColor != changeColor)
        {
            targetMaterial.SetColor(changeColorProperty, changeColor);
            _lastAppliedColor = changeColor;
        }
    }

    // 전체 순회 제거: 활성 픽셀만 감소
    private void FadeStepHeatActiveOnly()
    {
        float delta = fadeSpeed * Time.deltaTime;

        for (int i = activeIndices.Count - 1; i >= 0; i--)
        {
            int index = activeIndices[i];
            float next = Mathf.Max(0f, stepHeatValues[index] - delta);

            if (!Mathf.Approximately(stepHeatValues[index], next))
            {
                stepHeatValues[index] = next;
                MarkDirty(index);
            }

            if (next <= 0f)
            {
                RemoveActiveIndexAt(i);
            }
        }
    }

    private void StampFootstep()
    {
        Vector3 samplePos = feetPoint.position + Vector3.down * 0.05f;
        Vector3Int cell = tilemap.WorldToCell(samplePos);

        if (!cachedBounds.Contains(cell))
            return;

        if (!tilemap.HasTile(cell))
            return;

        // 실제 타일 좌표를 제한된 heat texture 좌표로 압축 매핑
        int x = cell.x - cachedBounds.xMin;
        int y = cell.y - cachedBounds.yMin;

        if (x < 0 || x >= width || y < 0 || y >= height)
            return;

        int texWidth = stepHeatTexture.width;
        int texHeight = stepHeatTexture.height;

        int mappedX = Mathf.Clamp(Mathf.FloorToInt((x / (float)width) * texWidth), 0, texWidth - 1);
        int mappedY = Mathf.Clamp(Mathf.FloorToInt((y / (float)height) * texHeight), 0, texHeight - 1);

        int index = mappedY * texWidth + mappedX;

        if (stepHeatValues[index] < 1f)
        {
            stepHeatValues[index] = 1f;
            MarkDirty(index);
        }

        AddActiveIndex(index);
    }

    // 전체 업로드 제거: 바뀐 픽셀만 업로드
    private void UploadDirtyPixelsOnly()
    {
        if (dirtyIndices.Count == 0)
            return;

        int texWidth = stepHeatTexture.width;

        for (int i = 0; i < dirtyIndices.Count; i++)
        {
            int index = dirtyIndices[i];
            int x = index % texWidth;
            int y = index / texWidth;

            float v = stepHeatValues[index];
            Color c = new Color(v, v, v, 1f);

            stepHeatPixels[index] = c;
            stepHeatTexture.SetPixel(x, y, c);
            dirtyLookup[index] = false;
        }

        dirtyIndices.Clear();
        stepHeatTexture.Apply(false, false);
    }

    private void MarkDirty(int index)
    {
        if (dirtyLookup[index])
            return;

        dirtyLookup[index] = true;
        dirtyIndices.Add(index);
    }

    private void AddActiveIndex(int index)
    {
        if (activeIndexLookup[index] != -1)
            return;

        activeIndexLookup[index] = activeIndices.Count;
        activeIndices.Add(index);
    }

    private void RemoveActiveIndexAt(int listIndex)
    {
        int removedIndex = activeIndices[listIndex];
        int lastListIndex = activeIndices.Count - 1;
        int lastIndex = activeIndices[lastListIndex];

        activeIndices[listIndex] = lastIndex;
        activeIndexLookup[lastIndex] = listIndex;

        activeIndices.RemoveAt(lastListIndex);
        activeIndexLookup[removedIndex] = -1;
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

    private void Start()
    {
        Bake();
    }
}
