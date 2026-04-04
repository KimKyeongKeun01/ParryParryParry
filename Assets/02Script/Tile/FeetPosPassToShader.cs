using UnityEngine;
using UnityEngine.Tilemaps;

public class FeetPosPassToShader : MonoBehaviour
{
    [SerializeField] private Tilemap tilemap;
    [SerializeField] private Transform feetPoint;
    [SerializeField] private Renderer tilemapRenderer;
    public Color changeClor;

    private Material mat;

    void Awake()
    {
        mat = tilemapRenderer.material;
    }

    void Update()
    {
        Vector3Int cell = tilemap.WorldToCell(feetPoint.position);

        if (tilemap.HasTile(cell))
        {
            Vector3 center = tilemap.GetCellCenterWorld(cell);

            mat.SetVector("_HighlightCellCenter", new Vector4(center.x, center.y, center.z, 0));
            mat.SetVector("_ChangeCellSize", new Vector2(tilemap.cellSize.x, tilemap.cellSize.y));
            mat.SetColor("_ChangeColor", changeClor);
        }
    }
}
