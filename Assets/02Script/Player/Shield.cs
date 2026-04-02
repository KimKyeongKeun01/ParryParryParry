using System;
using System.Collections;
using UnityEngine;

public enum ShieldState
{
    Equipped,
    Flying,
    Stuck,
    Recalling
}

public class Shield : MonoBehaviour
{
    private PlayerStatus stat;
    private Transform playerTransform;

    private Rigidbody2D rb;
    private Collider2D col;

    public Action Returned;

    public ShieldState CurrentState { get; private set; } = ShieldState.Equipped;

    public bool IsEquipped => CurrentState == ShieldState.Equipped;
    public bool IsFlying => CurrentState == ShieldState.Flying;
    public bool IsStuck => CurrentState == ShieldState.Stuck;
    public bool IsRecalling => CurrentState == ShieldState.Recalling;

    // 캐싱 (yield return new 금지)
    private WaitForFixedUpdate cachedWaitForFixedUpdate;

    private BoxCollider2D boxCollider2D;

    private Coroutine autoRecallCor;

    void Awake()
    {
        boxCollider2D = GetComponent<BoxCollider2D>();
    }

    public void Init(PlayerStatus _stat, Transform _playerTransform)
    {
        stat = _stat;
        playerTransform = _playerTransform;
        rb = GetComponent<Rigidbody2D>();
        col = GetComponent<Collider2D>();
        cachedWaitForFixedUpdate = new WaitForFixedUpdate();

        CurrentState = ShieldState.Equipped;
        gameObject.SetActive(false);
    }

    /// <summary>startPos: 장착 방패 위치에서 시작</summary>
    public void Throw(Vector2 startPos, Vector2 dir, float speed)
    {
        if (CurrentState != ShieldState.Equipped) return;

        transform.position = startPos;
        gameObject.SetActive(true);

        rb.bodyType = RigidbodyType2D.Dynamic;
        rb.linearVelocity = dir.normalized * speed;

        CurrentState = ShieldState.Flying;

        StartAutoRecallTimer();
    }

    private void StartAutoRecallTimer()
    {
        if(autoRecallCor != null) StopCoroutine(autoRecallCor);

        autoRecallCor = StartCoroutine(AutoRecallCoroutine());
    }

    private IEnumerator AutoRecallCoroutine()
    {
        yield return new WaitForSeconds(stat.ShieldAutoRecallDelay);

        if(CurrentState == ShieldState.Flying)
        {
            Recall(null);
        }
    }

    /// <summary>스테이지 전환 등 강제 초기화 시 호출 — 진행 중인 상태 무관하게 장착 상태로 복귀</summary>
    public void ForceEquip()
    {
        StopAllCoroutines();
        autoRecallCor = null;
        if (rb != null) rb.linearVelocity = Vector2.zero;
        CurrentState = ShieldState.Equipped;
        gameObject.SetActive(false);
    }

    private void StopAutoRecallTimer()
    {
        if(autoRecallCor != null)
        {
            StopCoroutine(autoRecallCor);
            autoRecallCor = null;
        }
    }

    public void Recall(Action onReturn)
    {
        if (CurrentState != ShieldState.Flying && CurrentState != ShieldState.Stuck) return;

        StartCoroutine(RecallCoroutine(onReturn));
    }

    private IEnumerator RecallCoroutine(Action onReturn)
    {
        bool wasStuck = CurrentState == ShieldState.Stuck;
        boxCollider2D.isTrigger = true;

        CurrentState = ShieldState.Recalling;
        rb.bodyType = RigidbodyType2D.Kinematic;
        rb.linearVelocity = Vector2.zero;

        // 벽에 박혀있었다면 흔들리다가 회수
        if (wasStuck)
        {
            yield return StartCoroutine(WobbleCoroutine());
            
        }

        // 플레이어를 향해 날아오기
        while (Vector2.Distance(transform.position, playerTransform.position) > 0.3f)
        {
            Vector2 dir = ((Vector2)playerTransform.position - (Vector2)transform.position).normalized;
            rb.MovePosition(rb.position + dir * stat.ShieldRecallSpeed * Time.fixedDeltaTime);
            yield return cachedWaitForFixedUpdate;
        }

        // 귀환 완료 — 콜백 먼저 호출 후 비활성화 (SetActive(false)는 코루틴을 종료시키므로 마지막에)
        
        rb.bodyType = RigidbodyType2D.Kinematic;
        rb.linearVelocity = Vector2.zero;
        boxCollider2D.isTrigger = false;

        CurrentState = ShieldState.Equipped;

        Returned?.Invoke();
        onReturn?.Invoke();
        gameObject.SetActive(false);
    }

    private IEnumerator WobbleCoroutine()
    {
        float elapsed = 0f;
        Vector3 originalPos = transform.position;

        while (elapsed < stat.ShieldWobbleDuration)
        {
            elapsed += Time.deltaTime;
            float wobble = Mathf.Sin(elapsed * 40f) * 0.15f;
            transform.position = originalPos + Vector3.right * wobble;
            yield return null;
        }

        transform.position = originalPos;
    }

    public void StickAt(Vector2 hitPoint)
    {
        StopAutoRecallTimer();

        transform.position = hitPoint;

        rb.bodyType = RigidbodyType2D.Static;
        rb.linearVelocity = Vector2.zero;

        CurrentState = ShieldState.Stuck;
    }

    private void OnCollisionEnter2D(Collision2D collision)
    {
        if (CurrentState != ShieldState.Flying) return;

        int hitLayer = collision.gameObject.layer;

        //Twall: 닿자마자 회수
        if((stat.ShieldAutoRecallLayer.value & (1 << hitLayer)) != 0)
        {
            Recall(null);
            return;
        }

        if ((stat.ShieldStickLayer.value & (1 << hitLayer)) != 0)
        {
            Vector2 hitPoint = collision.contacts[0].point;
            StickAt(hitPoint);
        }
    }
}
