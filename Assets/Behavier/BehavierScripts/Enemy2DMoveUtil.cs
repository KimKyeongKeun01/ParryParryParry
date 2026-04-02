using UnityEngine;

public static class Enemy2DMoveUtil
{
    public static bool CanMove(
        Transform self,
        float dirX,
        LayerMask groundMask,
        float wallCheckDistance,
        float ledgeCheckForward,
        float ledgeCheckDownDistance,
        Vector2 wallCheckOffset,
        Vector2 ledgeCheckOffset)
    {
        Vector2 direction = dirX > 0 ? Vector2.right : Vector2.left;

        // 1. 앞 벽 체크
        Vector2 wallOrigin = (Vector2)self.position + new Vector2(wallCheckOffset.x * Mathf.Sign(dirX), wallCheckOffset.y);
        RaycastHit2D wallHit = Physics2D.Raycast(wallOrigin, direction, wallCheckDistance, groundMask);
        if (wallHit.collider != null)
            return false;

        // 2. 절벽 체크: 앞쪽 아래에 바닥이 있는지
        Vector2 ledgeOrigin = (Vector2)self.position
                            + new Vector2(ledgeCheckOffset.x * Mathf.Sign(dirX), ledgeCheckOffset.y);

        RaycastHit2D groundHit = Physics2D.Raycast(ledgeOrigin, Vector2.down, ledgeCheckDownDistance, groundMask);
        if (groundHit.collider == null)
            return false;

        return true;
    }
}
