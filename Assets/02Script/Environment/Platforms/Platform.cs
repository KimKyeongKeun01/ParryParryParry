using UnityEngine;

public class Platform : MonoBehaviour
{
    public bool IsPassable { get; private set; } = false;
    public Color platformColor = Color.white;

    private void Awake()
    {
        // 초기에는 통과 불가능한 플랫폼으로 설정
        SetPassable(false);
        SetPlatformColor(platformColor);
    }

    public void SetPassable(bool passable)
    {
        IsPassable = passable;

        // 통과 가능 여부에 따라 콜라이더의 트리거 설정
        Collider2D collider = GetComponent<Collider2D>();
        if (collider != null)
        {
            collider.isTrigger = passable;
        }
    }

    public void SetPlatformColor(Color color)
    {
        SpriteRenderer renderer = GetComponent<SpriteRenderer>();
        if (renderer != null)
        {
            renderer.color = color;
        }
    }

    public Color GetPlatformColor()
    {
        SpriteRenderer renderer = GetComponent<SpriteRenderer>();
        if (renderer != null)
        {
            return renderer.color;
        }


        return Color.white;
    }
}
