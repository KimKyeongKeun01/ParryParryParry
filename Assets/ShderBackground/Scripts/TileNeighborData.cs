using UnityEngine;
using UnityEngine.Tilemaps;
using static UnityEditor.PlayerSettings;

public class TileNeighborData : MonoBehaviour
{
    [SerializeField] private Tilemap tilemap;
    [SerializeField] private Material targetMaterial;

    [Header("Shader Property Names")]
    [SerializeField] private string tileDataTexProperty = "_TileDataTex";
    [SerializeField] private string tilemapOriginProperty = "_TilemapOrigin";
    [SerializeField] private string tilemapSizeProperty = "_TilemapSize";
    [SerializeField] private string cellSizeProperty = "_CellSize";
    
    [Header("Player Standing Pos")]
    [SerializeField] private Transform feetPoint;
    [SerializeField] private Renderer tilemapRenderer;
    public Color changeClor;

    private Texture2D dataTexture;

    public void Bake()
    {
        if (tilemap == null || targetMaterial == null)
        {
            Debug.LogWarning("Tilemap or Material is missing.");
            return;
        }
        tilemap.CompressBounds();
        BoundsInt bounds = tilemap.cellBounds;
        int width = bounds.size.x;
        int height = bounds.size.y;

        if (width <= 0 || height <= 0)
        {
            Debug.LogWarning("Tilemap bounds are empty.");
            return;
        }

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
                Vector3Int pos = new Vector3Int(bounds.xMin + x, bounds.yMin + y, 0);

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

        dataTexture.Apply();

        Vector3 cellSize = tilemap.cellSize;
        Vector3 originWorld = tilemap.CellToWorld(new Vector3Int(bounds.xMin, bounds.yMin, 0));

        targetMaterial.SetTexture(tileDataTexProperty, dataTexture);
        targetMaterial.SetVector(tilemapOriginProperty, new Vector2(originWorld.x, originWorld.y));
        targetMaterial.SetVector(tilemapSizeProperty, new Vector2(width, height));
        targetMaterial.SetVector(cellSizeProperty, new Vector2(cellSize.x, cellSize.y));
    }

    private void Start()
    {
        Bake();
    }
    void Update()
    {
        Vector3Int cell = tilemap.WorldToCell(feetPoint.position);

        if (tilemap.HasTile(cell))
        {
            Vector3 center = tilemap.GetCellCenterWorld(cell);

            targetMaterial.SetVector("_HighlightCellCenter", new Vector4(center.x, center.y, center.z, 0));
            targetMaterial.SetVector("_ChangeCellSize", new Vector2(tilemap.cellSize.x, tilemap.cellSize.y));
            targetMaterial.SetColor("_ChangeColor", changeClor);
        }
        else
        {
            targetMaterial.SetVector("_HighlightCellCenter", new Vector4(0, 0, 0, 0));
            targetMaterial.SetVector("_ChangeCellSize", new Vector2(0, 0));
        }
    }
#if UNITY_EDITOR
    private void OnValidate()
    {
        if (!Application.isPlaying)
        {
            Bake();
        }
    }
#endif
}
