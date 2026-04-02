using System.Collections;

public interface IEnemy
{
    /// <summary>하위 클래스에서 대기 로직 구현</summary>
    void OnIdle();
    /// <summary>하위 클래스에서 이동 로직 구현</summary>
    void OnMove();
    /// <summary>하위 클래스에서 공격 로직 구현</summary>
    void OnAttack();
    /// <summary>하위 클래스에서 기절 로직 구현</summary>
    void OnStun(float duration);
    /// <summary>하위 클래스에서 탐지 로직 구현</summary>
    bool IsDetect();
}