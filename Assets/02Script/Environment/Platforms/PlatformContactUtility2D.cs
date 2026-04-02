using UnityEngine;

public static class PlatformContactUtility2D
{
    public static CapsuleDirection2D GetCapsuleDirectionFromSize(Vector2 size)
    {
        return size.x > size.y
            ? CapsuleDirection2D.Horizontal
            : CapsuleDirection2D.Vertical;
    }

    public static CapsuleDirection2D GetCapsuleDirectionFromWorldSize(CapsuleCollider2D capsuleCollider)
    {
        if (capsuleCollider == null)
        {
            return CapsuleDirection2D.Vertical;
        }

        Vector3 lossyScale = capsuleCollider.transform.lossyScale;
        Vector2 worldSize = new Vector2(
            Mathf.Abs(capsuleCollider.size.x * lossyScale.x),
            Mathf.Abs(capsuleCollider.size.y * lossyScale.y));

        return GetCapsuleDirectionFromSize(worldSize);
    }

    public static bool SyncCapsuleDirection(CapsuleCollider2D capsuleCollider)
    {
        if (capsuleCollider == null)
        {
            return false;
        }

        CapsuleDirection2D desiredDirection = GetCapsuleDirectionFromWorldSize(capsuleCollider);
        if (capsuleCollider.direction == desiredDirection)
        {
            return false;
        }

        capsuleCollider.direction = desiredDirection;
        return true;
    }

    public static void SyncCapsuleDirections(Collider2D[] colliders)
    {
        if (colliders == null)
        {
            return;
        }

        for (int i = 0; i < colliders.Length; i++)
        {
            if (colliders[i] is CapsuleCollider2D capsuleCollider)
            {
                SyncCapsuleDirection(capsuleCollider);
            }
        }
    }

    // 플랫폼 스크립트에서 공통으로 쓰는 접촉 대상의 루트 Transform/Rigidbody2D를 찾는다.
    public static bool ResolveRoot(Collider2D other, out Transform root, out Rigidbody2D body)
    {
        root = null;
        body = null;

        if (other == null)
        {
            return false;
        }

        if (other.attachedRigidbody != null)
        {
            body = other.attachedRigidbody;
            root = body.transform;
            return true;
        }

        root = other.transform.root != null ? other.transform.root : other.transform;
        if (root == null)
        {
            return false;
        }

        body = root.GetComponent<Rigidbody2D>();
        return true;
    }

    // 특정 태그에만 반응해야 하는 플랫폼용으로 태그 필터를 추가 적용한다.
    public static bool TryResolveTaggedTarget(Collider2D other, string tag, out Transform root, out Rigidbody2D body)
    {
        root = null;
        body = null;

        if (!ResolveRoot(other, out Transform resolvedRoot, out Rigidbody2D resolvedBody))
        {
            return false;
        }

        if (!string.IsNullOrEmpty(tag) && !resolvedRoot.CompareTag(tag))
        {
            return false;
        }

        root = resolvedRoot;
        body = resolvedBody;
        return true;
    }
}
