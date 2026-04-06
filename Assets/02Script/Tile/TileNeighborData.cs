using System.Collections.Generic;
using TreeEditor;
using UnityEngine;
using UnityEngine.Tilemaps;
using static UnityEditor.PlayerSettings;

public class TileNeighborData : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private Tilemap tilemap;
    [SerializeField] private Material targetMaterial;
    [SerializeField] private Transform feetPoint;

    [Header("�����¿� Ÿ�� ���")]
    [SerializeField] private string tileDataTexProperty = "_TileDataTex";
    [SerializeField] private string tilemapOriginProperty = "_TilemapOrigin";
    [SerializeField] private string tilemapSizeProperty = "_TilemapSize";
    [SerializeField] private string cellSizeProperty = "_CellSize";

    [Header("���ڱ�")]
    [SerializeField] private string stepHeatTexProperty = "_StepHeatTex";
    [SerializeField] private string changeColorProperty = "_ChangeColor";
    [SerializeField] private Color changeColor = Color.cyan;
    [SerializeField] private float fadeSpeed = 1.5f;
    [SerializeField] private int maxHeatTextureWidth = 512;
    [SerializeField] private int maxHeatTextureHeight = 100;
    [SerializeField] private float sampleOffsetY = 0.05f;
    
    [Header("����")]
    [SerializeField] private float waveSpeed = 10f;
    [SerializeField] private Color effectColor = Color.cyan;
    [SerializeField] private float centerWorldX = 0f;
    [SerializeField] private float radiusWorld = 1f;
    [SerializeField] private float totalDuration = 1f;
    [SerializeField] private float spreadSoftness = .4f;
    [SerializeField] private float rowHalfHeight = 1f;
    [SerializeField] private float rowSoftness = .4f;
    [Header("�׽�Ʈ ��")]
    [SerializeField] private Transform playerFeetPos;

    private Texture2D dataTexture;
    private Texture2D stepHeatTexture;

    private BoundsInt cachedBounds;
    private int width;
    private int height;

    private float[] stepHeatValues;

    private readonly List<int> activeIndices = new();
    private int[] activeIndexLookup;

    private readonly List<int> dirtyIndices = new();
    private bool[] dirtyLookup;

    private Color _lastAppliedColor = Color.clear;
    private Vector2 _lastOriginWorld;
    private Vector2 _lastCellSize;
    private Vector2 _lastTilemapSize;

    private bool hasValidReferences;

    // width/height -> heat texture ��ǥ ���ο� ĳ��
    private float heatScaleX;
    private float heatScaleY;

    private void Awake()
    {
        CacheReferenceState();
    }

    private void Start()
    {
        Bake();
        Player.Instance.controller.onSlamImpact += PlayWave;
    }
    private void CacheReferenceState()
    {
        hasValidReferences = tilemap != null && targetMaterial != null && feetPoint != null;
    }
    public void PlayWave(Vector2 j, Vector2 i,Color _)
    {
        Vector3 skillStartWorldPos = playerFeetPos.position;

        targetMaterial.SetFloat("_WaveSpeed", waveSpeed);
        targetMaterial.SetFloat("_StartTime", Time.time);
        targetMaterial.SetColor("_EffectColor", effectColor);
        targetMaterial.SetFloat("_CenterWorldX", skillStartWorldPos.x);
        targetMaterial.SetFloat("_RadiusWorld", radiusWorld);
        targetMaterial.SetFloat("_TotalDuration", totalDuration);
        targetMaterial.SetFloat("_SpreadSoftness", spreadSoftness);
        targetMaterial.SetFloat("_CenterWorldY", skillStartWorldPos.y);
        targetMaterial.SetFloat("_RowHalfHeight", rowHalfHeight);
        targetMaterial.SetFloat("_RowSoftness", rowSoftness);
    }
    public void Bake()
    {
        CacheReferenceState();

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
            if (dataTexture != null)
                DestroyTexture(dataTexture);

            dataTexture = new Texture2D(width, height, TextureFormat.RGBA32, false, true)
            {
                filterMode = FilterMode.Point,
                wrapMode = TextureWrapMode.Clamp,
                name = "Tilemap_Data_Texture"
            };
        }

        for (int y = 0; y < height; y++)
        {
            int cellY = cachedBounds.yMin + y;

            for (int x = 0; x < width; x++)
            {
                int cellX = cachedBounds.xMin + x;
                Vector3Int pos = new(cellX, cellY, 0);

                if (!tilemap.HasTile(pos))
                {
                    dataTexture.SetPixel(x, y, Color.clear);
                    continue;
                }

                bool up = tilemap.HasTile(pos + Vector3Int.up);
                bool right = tilemap.HasTile(pos + Vector3Int.right);
                bool down = tilemap.HasTile(pos + Vector3Int.down);
                bool left = tilemap.HasTile(pos + Vector3Int.left);

                Color data = new(
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
        int texWidth = Mathf.Min(width, maxHeatTextureWidth);
        int texHeight = Mathf.Min(height, maxHeatTextureHeight);

        bool needCreate =
            stepHeatTexture == null ||
            stepHeatTexture.width != texWidth ||
            stepHeatTexture.height != texHeight ||
            stepHeatValues == null ||
            stepHeatValues.Length != texWidth * texHeight ||
            activeIndexLookup == null ||
            activeIndexLookup.Length != texWidth * texHeight ||
            dirtyLookup == null ||
            dirtyLookup.Length != texWidth * texHeight;

        if (!needCreate)
        {
            RecalculateHeatScales(texWidth, texHeight);
            targetMaterial.SetTexture(stepHeatTexProperty, stepHeatTexture);
            return;
        }

        if (stepHeatTexture != null)
            DestroyTexture(stepHeatTexture);

        stepHeatTexture = new Texture2D(texWidth, texHeight, TextureFormat.RGBA32, false, true)
        {
            filterMode = FilterMode.Point,
            wrapMode = TextureWrapMode.Clamp,
            name = "Tilemap_StepHeat_Texture"
        };

        int pixelCount = texWidth * texHeight;

        stepHeatValues = new float[pixelCount];
        activeIndexLookup = new int[pixelCount];
        dirtyLookup = new bool[pixelCount];

        activeIndices.Clear();
        dirtyIndices.Clear();

        for (int i = 0; i < pixelCount; i++)
        {
            stepHeatValues[i] = 0f;
            activeIndexLookup[i] = -1;
            dirtyLookup[i] = false;
        }

        ClearHeatTexture(texWidth, texHeight);
        RecalculateHeatScales(texWidth, texHeight);

        targetMaterial.SetTexture(stepHeatTexProperty, stepHeatTexture);
    }

    private void RecalculateHeatScales(int texWidth, int texHeight)
    {
        heatScaleX = width > 0 ? texWidth / (float)width : 0f;
        heatScaleY = height > 0 ? texHeight / (float)height : 0f;
    }

    private void ClearHeatTexture(int texWidth, int texHeight)
    {
        Color32[] clear = new Color32[texWidth * texHeight];
        stepHeatTexture.SetPixels32(clear);
        stepHeatTexture.Apply(false, false);
    }

    private void ApplyCommonMaterialProperties()
    {
        Vector3 cellSize3 = tilemap.cellSize;
        Vector3 originWorld3 = tilemap.CellToWorld(new Vector3Int(cachedBounds.xMin, cachedBounds.yMin, 0));

        Vector2 originWorld = new(originWorld3.x, originWorld3.y);
        Vector2 cellSize = new(cellSize3.x, cellSize3.y);
        Vector2 tilemapSize = new(width, height);

        if (_lastOriginWorld != originWorld)
        {
            targetMaterial.SetVector(tilemapOriginProperty, originWorld);
            _lastOriginWorld = originWorld;
        }

        if (_lastTilemapSize != tilemapSize)
        {
            targetMaterial.SetVector(tilemapSizeProperty, tilemapSize);
            _lastTilemapSize = tilemapSize;
        }

        if (_lastCellSize != cellSize)
        {
            targetMaterial.SetVector(cellSizeProperty, cellSize);
            _lastCellSize = cellSize;
        }

        if (_lastAppliedColor != changeColor)
        {
            targetMaterial.SetColor(changeColorProperty, changeColor);
            _lastAppliedColor = changeColor;
        }
    }

    private void Update()
    {
        if (!hasValidReferences)
            return;

        if (stepHeatTexture == null || stepHeatValues == null)
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


    private void FadeStepHeatActiveOnly()
    {
        float delta = fadeSpeed * Time.deltaTime;

        for (int i = activeIndices.Count - 1; i >= 0; i--)
        {
            int index = activeIndices[i];
            float next = stepHeatValues[index] - delta;

            if (next <= 0f)
            {
                stepHeatValues[index] = 0f;
                MarkDirty(index);
                RemoveActiveIndexAt(i);
                continue;
            }

            if (!Mathf.Approximately(stepHeatValues[index], next))
            {
                stepHeatValues[index] = next;
                MarkDirty(index);
            }
        }
    }

    private void StampFootstep()
    {
        Vector3 samplePos = feetPoint.position;
        samplePos.y -= sampleOffsetY;

        Vector3Int cell = tilemap.WorldToCell(samplePos);

        if (!cachedBounds.Contains(cell))
            return;

        if (!tilemap.HasTile(cell))
            return;

        int x = cell.x - cachedBounds.xMin;
        int y = cell.y - cachedBounds.yMin;

        if ((uint)x >= (uint)width || (uint)y >= (uint)height)
            return;

        int texWidth = stepHeatTexture.width;
        int texHeight = stepHeatTexture.height;

        int mappedX = Mathf.Min((int)(x * heatScaleX), texWidth - 1);
        int mappedY = Mathf.Min((int)(y * heatScaleY), texHeight - 1);

        int index = mappedY * texWidth + mappedX;

        if (stepHeatValues[index] < 1f)
        {
            stepHeatValues[index] = 1f;
            MarkDirty(index);
        }

        AddActiveIndex(index);
    }

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

            byte v = (byte)Mathf.RoundToInt(stepHeatValues[index] * 255f);
            stepHeatTexture.SetPixel(x, y, new Color32(v, v, v, 255));
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

        if (listIndex != lastListIndex)
        {
            int lastIndex = activeIndices[lastListIndex];
            activeIndices[listIndex] = lastIndex;
            activeIndexLookup[lastIndex] = listIndex;
        }

        activeIndices.RemoveAt(lastListIndex);
        activeIndexLookup[removedIndex] = -1;
    }



#if UNITY_EDITOR
    private void OnValidate()
    {
        CacheReferenceState();

        if (!Application.isPlaying && tilemap != null && targetMaterial != null)
        {
            Bake();
        }
    }
#endif

    private void OnDisable()
    {
        hasValidReferences = false;
    }

    private void OnDestroy()
    {
        Player.Instance.controller.onSlamImpact -= PlayWave;
        DestroyTexture(dataTexture);
        DestroyTexture(stepHeatTexture);
    }

    private void DestroyTexture(Texture2D tex)
    {
        if (tex == null)
            return;

        if (Application.isPlaying)
            Destroy(tex);
        else
            DestroyImmediate(tex);
    }
}
