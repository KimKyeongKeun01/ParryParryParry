using UnityEngine;
using UnityEngine.Playables;

/// <summary>
/// 보스방 전용 스테이지 스크립트.
/// Stage 스크립트와 별개로, 보스 기믹에 사용하는 플랫폼 활성화/비활성화를 담당한다.
/// Boss와 상호 참조한다.
/// </summary>
public class BossStage : MonoBehaviour
{
    [Header("Boss Reference")]
    [SerializeField] private Stage stage;
    [SerializeField] private GameObject bossObj;
    [SerializeField] private BaseBoss bossScript;
    [SerializeField] private Transform spawnPoint;

    [Header("Stage Door")]
    [SerializeField] private Platform_Moving entrance_Door;
    [SerializeField] private Platform_Moving exit_Door;

    [Header("Timeline")]
    [SerializeField] private PlayableDirector bossIntro;
    private bool isCutscenePlaying = false;
    private bool hasBossStarted = false;


    private bool isBossBattle = false;
    private bool stageCleared = false;
    

    private void OnEnable() 
    { 
        Stage.OnPlayerEnteredStage += OnStageEntered;
        if (bossIntro != null) bossIntro.stopped += OnIntroFinished;
    }
    private void OnDisable() 
    { 
        Stage.OnPlayerEnteredStage -= OnStageEntered;
        if (bossIntro != null) bossIntro.stopped -= OnIntroFinished;
    }

    private void Start()
    {
        // 초기 상태 설정
        if (bossObj != null) bossObj.SetActive(false);

        // 출입구 세팅
        if (entrance_Door != null) entrance_Door.SetExternalSignal(false);
        if (exit_Door != null) exit_Door.SetExternalSignal(false);

        // 타임라인 세팅
        if (bossIntro != null)
        {
            bossIntro.time = 0;
            bossIntro.Stop();
        }
    }

    private void Update()
    {
        if (!isBossBattle || stageCleared) return;

        // 보스 사망 체크
        if (bossScript != null && bossScript.isDead)
        {
            BossClear();
        }
    }

    private void OnStageEntered(Stage enteredStage)
    {
        if (enteredStage != stage) return;
        // 보스전 시작 전의 배경음악 변경이나 UI 알림 등을 여기에 넣을 수 있음
    }

    public void StartBoss()
    {
        if (isBossBattle || stageCleared) return;
        hasBossStarted = true;
        Debug.Log($"[BossStage] {gameObject.name}: Boss Intro Start.");

        // 1. 입구 문 닫기
        if (entrance_Door != null)
        {
            entrance_Door.SetExternalSignal(true);
        }

        // 2. 보스 컷신
        if (bossIntro != null)
        {
            isCutscenePlaying = true;

            bossIntro.time = 0;
            bossIntro.Evaluate();
            bossIntro.Play();
        }
        else
        {
            StartBattle();
        }
    }

    public void OnIntroFinished(PlayableDirector director)
    {
        if (director != bossIntro) return;
        if (!isCutscenePlaying || stageCleared) return;

        Debug.Log("[Boss Stage] Boss Intro Finished.");

        StartBattle();
    }


    private void StartBattle()
    {
        if (isBossBattle) return;

        isBossBattle = true;
        isCutscenePlaying = false;

        Debug.Log($"[BossStage] {gameObject.name}: Boss Battle Start!");

        // 보스 위치 설정 및 활성화
        if (bossObj != null && spawnPoint != null)
        {
            bossObj.transform.position = spawnPoint.position;
            bossObj.SetActive(true);
        }
    }

    public void BossClear()
    {
        if (stageCleared) return;
        stageCleared = true;

        Debug.Log($"[BossStage] {gameObject.name}: Boss Clear!");

        // 3. StageManager 클리어 알림
        if (StageManager.Instance != null)
        {
            StageManager.Instance.ClearCurrentStage();
        }

        // 4. 출구 문 열기
        if (exit_Door != null)
        {
            exit_Door.SetExternalSignal(true);
        }

        // 5. 입구 문 다시 열기 (선택 사항)
        if (entrance_Door != null) entrance_Door.SetExternalSignal(false);
    }
}
