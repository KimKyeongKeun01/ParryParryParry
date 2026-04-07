using System.Collections;
using UnityEngine;

public class Enemy_Monkey : BaseEnemy
{
    [Header(" === Monkey ===")]
    [Header("Movement")]
    [SerializeField] private float moveSpeed = 5f;
    [SerializeField] private float detectRangeLimit = 8f;   // 투척 시작 거리
    [SerializeField] private float evadeRange = 3.5f;       // 이 거리 안에 오면 회피
    [SerializeField] private float evadeLength = 5f;    // 회피 시 한 번에 이동할 거리
    [SerializeField] private float evadeCooldown = 1.5f;    // 회피 쿨타임
    private const float EVADE_DURATION = 0.3f;          // 회피에 걸리는 시간 (고정)
    private float lastEvadeTime = -99f;
    [SerializeField] private bool isEvading = false;
    private bool isForcedEvade = false;

    [Header("Banana Throw")]
    [SerializeField] private GameObject bananaPrefab;
    [SerializeField] private Transform throwPoint;
    [SerializeField] private float throwInterval = 2.5f;
    [SerializeField] private float bananaSpeed = 12f;
    [SerializeField] private float bananaApexOffset = 1.5f;
    private float lastThrowTime;

    [Header("Obstacle Check")]
    [SerializeField] private float wallCheckDistance = 1.0f;
    [SerializeField] private float edgeCheckForwardOffset = 0.8f;
    [SerializeField] private float edgeCheckDistance = 1.5f;

    [Header("Visual")]
    [SerializeField] private SpriteRenderer _sprite;
    [SerializeField] private Color windupColor = Color.red;
    [SerializeField] private Color stunColor;
    private Color originColor;

    private Coroutine evadeRoutine;
    [SerializeField] private bool canNotMove = false;
    private Vector2 evadePos;
    private Vector2 startPos;
    private float evadeTimer = 0f;

    protected override void Awake()
    {
        base.Awake();
        if (_sprite != null) originColor = _sprite.color;

        // 키네마틱 설정 (우탄과 동일하게 물리 간섭 최소화)
        _rigid.bodyType = RigidbodyType2D.Kinematic;
        _rigid.useFullKinematicContacts = true;
    }

    protected override void Update()
    {
        if (isDead) return;

        if (isEvading) return;
        // 스턴 상태가 아닐 때만 회피 및 거리 유지 로직 작동
        if (CurState != EnemyState.Stun && !isEvading)
        {
            HandleAI();
        }

        base.Update();
    }
    private void FixedUpdate()
    {
        if (isEvading)
        {
            ExecuteEvade();
        }

    }
    private void ExecuteEvade()
    {
        evadeTimer += Time.fixedDeltaTime;
        float progress = evadeTimer / EVADE_DURATION;

        if (progress < 1.0f)
        {
            Vector2 nextPos = Vector2.Lerp(startPos, evadePos, progress);
            _rigid.MovePosition(nextPos);
        }
        else
        {
            _rigid.MovePosition(evadePos);
            isEvading = false;
        }
        lastEvadeTime = Time.time;
    }

    private void HandleAI()
    {
        Player player = Player.Instance;
        if (player == null) return;

        float dist = Vector2.Distance(transform.position, player.transform.position);

        // 1. 회피 로직 (플레이어가 너무 가까움)
        if (dist <= evadeRange && Time.time - lastEvadeTime >= evadeCooldown)
        {
            TryStartEvade(player.transform.position);

            return;
        }

        // 2. 공격 로직 (적정 거리 유지 중)
        if (dist <= detectRangeLimit)
        {
            ChangeState(EnemyState.Attack);
        }
        else
        {
            ChangeState(EnemyState.Move);
        }
    }

    #region 회피 및 이동
    private void TryStartEvade(Vector2 playerPos)
    {
        if (isDead) return;
        if (isEvading || isForcedEvade) return;
        if (evadeRoutine != null) return;
        evadePosCheck(playerPos);
        //evadeRoutine = StartCoroutine(Co_Evade(playerPos));
    }

    

    private void evadePosCheck(Vector2 playerPos)
    {
        startPos = transform.position;
        evadeTimer = 0f;
        ChangeState(EnemyState.Idle);
        float evadeDir = Mathf.Sign(transform.position.x - playerPos.x);
       
        if (evadeDir == 0)
            evadeDir = facingX == 0 ? 1f : -facingX;
        facingX = evadeDir;
        UpdateFlip();

        Debug.Log(evadeDir);
        Debug.Log(facingX);

        if (canNotMove) // canNotMove가 true인 상태에서 다시 이동 메서드 요청이 들어올 시에는 일단 evadeDir을 -1*해서 반대로 바꾸고 쏨
        { 
            facingX *= -1; 
            canNotMove = false; 
            UpdateFlip(); 
            
        } //마지막 회피 체크가 불가능이었을 시 방향 바꿈
        RaycastHit2D hit = Physics2D.Raycast(new Vector2 (transform.position.x+(facingX*1f),transform.position.y), Vector2.right*facingX, evadeLength, groundLayer); // 레이를 transform.position에서 evadeLength만큼 evadeDir방향만큼 쏨

        if (hit.collider != null) //걸린게 있다면 해당 distance만큼이 이동가능 거리
        {
            canNotMove = true; // evadeLength와 실제 이동 distance가 다를경우 canNotMove는 true로 바꾸고 일단 실제 거리만큼 이동함
            evadePos = _rigid.position + (Vector2.right * facingX * hit.distance);
        }
        else //// 걸린게 없다면 transform.position.x+evadeLength*facingX, transform.position.y-1.5(플레이어 길이) 지점에서 레이를 -evadeDir방향으로 range 만큼 쏨
        {
            Vector2 downPos = new Vector2(transform.position.x + (facingX *evadeLength), transform.position.y - 1.5f);
            RaycastHit2D downHit = Physics2D.Raycast(downPos, Vector2.right * facingX * (-1), evadeLength, groundLayer);
            Debug.DrawRay(downPos, Vector2.right * facingX * (-1) * evadeLength, Color.yellow, 1.0f);
            if (Mathf.Abs( downHit.distance) > 0) //// 두번째 레이의 distance가 0보다 크지 않다면 절벽임
            {
                canNotMove = true;
                evadePos = _rigid.position + (Vector2.right * facingX * (evadeLength-downHit.distance));//evadeLength-distance만큼이 이동가능한 실제 거리
            }
            else
            {
                evadePos = _rigid.position + (Vector2.right * facingX * evadeLength);// 0이면 문제 없음 range만큼 이동 가능
            }
        }
        isEvading = true;
    }

    //예전 회피 코루틴 (FixedUpdate로 바꿔보려고 일단 보류)
    private IEnumerator Co_Evade(Vector2 playerPos)
    {
        isEvading = true;
        isForcedEvade = false;
        lastEvadeTime = Time.time;
        ChangeState(EnemyState.Idle);

        float evadeDir = Mathf.Sign(transform.position.x - playerPos.x);
        if (evadeDir == 0)
            evadeDir = facingX == 0 ? 1f : -facingX;

        facingX = -evadeDir;
        UpdateFlip();

        Vector2 startPos = _rigid.position;
        Vector2 endPos = startPos + Vector2.right * evadeDir * evadeLength;

        bool redirected = false;
        float elapsed = 0f;

        while (elapsed < EVADE_DURATION)
        {
            if (!redirected && (IsWall(evadeDir) || IsEdge(evadeDir)))
            {
                redirected = true;
                isForcedEvade = true;

                startPos = _rigid.position;
                endPos = new Vector2(
                    playerPos.x + evadeLength * -facingX,
                    startPos.y
                );

                elapsed = 0f;
                continue;
            }

            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / EVADE_DURATION);

            Vector2 nextPos = Vector2.Lerp(startPos, endPos, t);
            _rigid.MovePosition(nextPos);

            yield return null;
        }

        _rigid.MovePosition(endPos);

        isForcedEvade = false;
        isEvading = false;
        evadeRoutine = null;
        ChangeState(EnemyState.Idle);
    }

    public override void OnMove()
    {
        Player player = Player.Instance;
        if (player == null) return;

        float dir = Mathf.Sign(player.transform.position.x - transform.position.x);
        if (dir == 0) dir = facingX == 0 ? 1f : facingX;

        if (IsWall(dir) || IsEdge(dir))
        {
            ChangeState(EnemyState.Idle);
            return;
        }

        facingX = dir;
        UpdateFlip();

        Vector2 nextPos = _rigid.position + Vector2.right * dir * moveSpeed * Time.deltaTime;
        _rigid.MovePosition(nextPos);
    }

    private void UpdateFlip()
    {
        if (_sprite != null) _sprite.flipX = facingX < 0;
    }
    #endregion

    #region 공격 (바나나 던지기)
    public override void OnAttack()
    {
        if (isEvading || Time.time - lastThrowTime < throwInterval) return;

        StartCoroutine(Co_ThrowSequence());
    }

    private IEnumerator Co_ThrowSequence()
    {
        lastThrowTime = Time.time;

        if (_sprite != null)
            _sprite.color = windupColor;

        yield return new WaitForSeconds(0.5f);

        if (_sprite != null)
            _sprite.color = originColor;

        ThrowBanana();
    }

    private void ThrowBanana()
    {
        if (bananaPrefab == null || throwPoint == null || Player.Instance == null) return;

        // 1. 투사체 소환
        GameObject banana = Instantiate(bananaPrefab, throwPoint.position, Quaternion.identity);
        BaseProjectile proj = banana.GetComponent<BaseProjectile>();

        if (proj != null)
        {
            // --- 물리 계산 부분 ---
            Vector2 startPos = throwPoint.position;
            Vector2 targetPos = Player.Instance.transform.position;

            float dx = targetPos.x - startPos.x;
            float dy = targetPos.y - startPos.y;

            // 너무 가까운 거리 예외 방지
            float totalTime = Mathf.Abs(dx) / bananaSpeed;
            if (totalTime < 0.15f) totalTime = 0.15f;

            // 최고점을 시작점/목표점보다 항상 위로 잡음
            float apexY = Mathf.Max(startPos.y, targetPos.y) + Mathf.Max(0.5f, bananaApexOffset);
            float h1 = apexY - startPos.y;   // 시작점 -> 최고점
            float h2 = apexY - targetPos.y;  // 목표점 -> 최고점

            // 안전장치
            h1 = Mathf.Max(0.01f, h1);
            h2 = Mathf.Max(0.01f, h2);

            // 중력 및 초기속도 계산
            float sqrtG = (Mathf.Sqrt(2f * h1) + Mathf.Sqrt(2f * h2)) / totalTime;
            float g = sqrtG * sqrtG;
            float vy = Mathf.Sqrt(2f * g * h1);
            float vx = dx / totalTime;

            // --- 핵심: 중력 활성화 ---
            // 1. 중력 계수 계산 (유니티 기본 중력 9.81 기준)
            float worldGravity = Mathf.Abs(Physics2D.gravity.y);
            if (worldGravity < 0.001f) worldGravity = 9.81f;

            float gravityScale = g / worldGravity;

            // 2. BaseProjectile에게 중력 사용 명령 (이게 호출되어야 FixedUpdate의 직선이동이 멈춤)
            proj.SetGravity(true, gravityScale);

            // 3. 투사체 초기화 (Init 내부에서 방향/데미지 세팅)
            Vector2 launchVelocity = new Vector2(vx, vy);
            proj.Init(launchVelocity.normalized, damage, launchVelocity.magnitude);

            // 4. Rigidbody에 실제 속도 부여 (SetGravity가 Dynamic으로 바꿨으므로 속도가 먹힘)
            Rigidbody2D rb = banana.GetComponent<Rigidbody2D>();
            if (rb == null) return;

            rb.linearVelocity = launchVelocity;
        }
    }
    #endregion

    #region 장애물 감지
    private bool IsWall(float dir)
    {
        // 콜라이더 중심에서 진행 방향으로 레이 발사
        return Physics2D.Raycast(transform.position, Vector2.right * dir, 5.0f, groundLayer);
    }

    private bool IsEdge(float dir)
    {
        // 진행 방향 앞쪽 바닥을 체크 (현재 위치 x + 방향 * 0.8f 지점에서 아래로)
        Vector2 checkPos = new Vector2(transform.position.x + (dir * 0.8f), transform.position.y);
        RaycastHit2D hit = Physics2D.Raycast(checkPos, Vector2.down, 1.5f, groundLayer);

        // 바닥이 없으면(null) 절벽임
        return hit.collider == null;
    }
    #endregion

    #region 피격 및 반사 판정
    public override void TakeDamage(int dmg)
    {
        // 스턴 상태(반사된 바나나를 맞음)에서만 데미지를 입음
        if (CurState == EnemyState.Stun)
        {
            base.TakeDamage(dmg);
        }
        else
        {
            // 일반 상태에서 맞으면 데미지는 안 입고 즉시 회피 시도
            Player player = Player.Instance;
            if (!isEvading && player != null)
            {
                //TryStartEvade(player.transform.position);
            }
        }
    }

    /// <summary> 바나나가 플레이어의 패링으로 반사되어 돌아왔을 때 호출됨 </summary>
    public void OnReflectHit(int dmg)
    {
        if (isDead) return;

        Debug.Log("[Monkey] Stunned by reflected banana!");
        ChangeState(EnemyState.Stun);
        if (_sprite != null) _sprite.color = stunColor;

        // 스턴 중에는 데미지 입음
        base.TakeDamage(dmg);

        // 일정 시간 후 회복
        StopAllCoroutines(); // 기존 회피나 공격 중단
        isEvading = false;
        StartCoroutine(Co_RecoverStun());
    }

    private IEnumerator Co_RecoverStun()
    {
        yield return new WaitForSeconds(stunDuration);
        if (_sprite != null) _sprite.color = originColor; 
        ChangeState(EnemyState.Idle);
    }
    #endregion

    //protected override void OnCollisionEnter2D(Collision2D collision)
    //{
    //    // 스턴 상태가 아닐 때 플레이어가 닿으려고 하면 회피
    //    if (CurState != EnemyState.Stun && collision.gameObject.CompareTag("Player"))
    //    {
    //        Player player = Player.Instance;
    //        if (player != null)
    //        {
    //            StartCoroutine(Co_IgnorePlayer(player, 1f));
    //            TryStartEvade(collision.transform.position);

    //            return;
    //        }
    //    }

    //    base.OnCollisionEnter2D(collision);
    //}

    #region 시각화
    protected override void OnDrawGizmos()
    {
        Vector2 pos = transform.position;
        float labelOffset = 0.5f;

        // --- 1. 투척 시작 범위 (Detect Range Limit) ---
        // 이 범위 안에 들어오면 원숭이가 바나나를 던지기 시작함
        Gizmos.color = Color.red;
        Gizmos.DrawWireSphere(pos, detectRangeLimit);
        

        // --- 2. 회피 범위 (Evade Range) ---
        // 이 범위 안에 플레이어가 들어오면 원숭이가 즉시 도망침 (가장 위험한 구역)
        Gizmos.color = Color.purple;
        Gizmos.DrawWireSphere(pos, evadeRange);
        

        // --- 4. 실시간 회피 경로 및 지형 체크 디버깅 ---
        if (Application.isPlaying && Player.Instance != null)
        {
            float dist = Vector2.Distance(pos, Player.Instance.transform.position);
            float evadeDir = Mathf.Sign(transform.position.x - Player.Instance.transform.position.x);

            // [회피 예상 경로] 설정된 evadeLength를 반영하여 표시
            Gizmos.color = Color.magenta;
            Vector3 targetPath = transform.position + Vector3.right * evadeDir * evadeLength;
            Gizmos.DrawRay(transform.position, Vector3.right * evadeDir * evadeLength);

            // 회피 후 도착할 예상 지점 (박스)
            Gizmos.DrawWireCube(targetPath, new Vector3(0.6f, 1.2f, 0.1f));
#if UNITY_EDITOR
            UnityEditor.Handles.Label(targetPath + Vector3.up * 1.5f, $"Evade Target ({evadeLength}m)", GetLabelStyle(Color.magenta));
#endif

            // [지형 체크 레이 시각화] IsEdge와 IsWall이 어디를 쏘고 있는지 확인
            // 절벽 체크 (Blue)
            Vector2 edgeOrigin = new Vector2(pos.x + (evadeDir * 0.8f), pos.y);
            Gizmos.color = Color.blue;
            Gizmos.DrawRay(edgeOrigin, Vector2.down * 1.5f);

            // 벽 체크 (White)
            Gizmos.color = Color.white;
            Gizmos.DrawRay(pos, Vector2.right * evadeDir * 1.0f);

            // 플레이어와의 시야선
            Gizmos.color = (dist <= detectRangeLimit) ? Color.green : Color.gray;
            Gizmos.DrawLine(pos, Player.Instance.transform.position);
        }

#if UNITY_EDITOR
        UnityEditor.Handles.Label(pos + Vector2.up * (detectRangeLimit + labelOffset), "Throw Range", GetLabelStyle(Color.cyan));
        UnityEditor.Handles.Label(pos + Vector2.up * (evadeRange + labelOffset), "Evade Zone", GetLabelStyle(Color.red));
#endif
    }

    // Boss_Utan과 동일한 스타일의 레이블 헬퍼
#if UNITY_EDITOR
    private GUIStyle GetLabelStyle(Color color)
    {
        GUIStyle style = new GUIStyle();
        style.normal.textColor = color;
        style.alignment = TextAnchor.MiddleCenter;
        style.fontStyle = FontStyle.Bold;
        style.fontSize = 11;
        return style;
    }
#endif
    #endregion
}
