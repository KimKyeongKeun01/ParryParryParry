using UnityEngine;

public interface IBounceable
{
    /// <summary>
    /// 바운스 장애물 충돌 시 호출. 반사된 속도를 전달한다.
    /// </summary>
    /// <param name="reflectedVelocity">법선 기준으로 반사된 속도 벡터</param>
    void OnBounce(Vector2 reflectedVelocity);//physicsmaterial2d의 bounciness1해주셔야합니다.
}
