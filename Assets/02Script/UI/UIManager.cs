using System;
using UnityEngine;
using UnityEngine.UI;

public class UIManager : MonoBehaviour
{
    public static UIManager Instance { get; private set; }

    [Header("Title")]
    public UITitle title;

    [Header("Panel")]
    public UIPause pause;
    public UIClear clear;

    [Header("Player")]
    [SerializeField] private GameObject healthBar;
    [SerializeField] private Icon[] healthIcon;
    [SerializeField] private Sprite fullHeart;
    [SerializeField] private Sprite emptyHeart;
    [Space(10)]
    [SerializeField] private UIGuard guardIcon;
    public UIGuard GuardIcon => guardIcon;

    [Header("Transition")]
    [SerializeField] private Fader fader;


    [SerializeField] private NoticeText noticeText;
    

    private void Awake()
    {
        if(Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        DontDestroyOnLoad(gameObject);
    }

    private void Start()
    {
        Init();

        if (GameManager.Instance == null)
        {
            SetHealthBar(true);
            SetGuard(true);

            UpdateHealth();
        }
    }

    private void Init()
    {
        title.Init();
        pause.Init();
        clear.Init();

        Debug.Log("[UI Manager] UI Init Complete");
    }

    #region 체력
    public void SetHealthBar(bool active)
    {
        if (healthBar != null) healthBar.SetActive(active);
    }

    public void UpdateHealthBar(int curHp, int maxHp, bool force = false)
    {
        for (int i = 0; i < healthIcon.Length; i++)
        {
            // 최대 체력 세팅
            if (i < maxHp)
            {
                healthIcon[i].gameObject.SetActive(true);
                // 현재 체력에 따라 이미지 세팅
                healthIcon[i].SetState(i < curHp, fullHeart, emptyHeart, force);
            }
            // 최대 체력 오버는 끄기
            else
            {
                healthIcon[i].gameObject.SetActive(false);
            }
        }
    }

    public void UpdateHealth(bool force = false)
    {
        Player player = Player.Instance;
        if (player == null) return;

        SetHealthBar(true);
        UpdateHealthBar(player.status.GetCurHp(), player.status.maxHp, force);

        UpdateGuard(player);
    }
    #endregion

    #region 가드
    public void SetGuard(bool active)
    {
        if (guardIcon == null) return;

        guardIcon.Init();
        guardIcon.gameObject.SetActive(active);
    }

    private void UpdateGuard(Player player)
    {
        var controller = player.controller;
        var uiIcon = GuardIcon;
        if (uiIcon == null || controller == null) return;

        // 가드 시작: Active
        controller.onGuardStart -= OnGuardStartUI;
        controller.onGuardStart += OnGuardStartUI;

        // 가드 종료: Cooldown
        controller.onGuardEnd -= OnGuardEndUI;
        controller.onGuardEnd += OnGuardEndUI;

        // 이벤트 등록
        controller.onGuardEnd -= () => uiIcon.SetState(UIGuard.GuardState.Cooldown);
        controller.onGuardEnd += () => uiIcon.SetState(UIGuard.GuardState.Cooldown);

        controller.onPerfectGuardSuccess -= OnPerfectGuardUI;
        controller.onPerfectGuardSuccess += OnPerfectGuardUI;

        SetGuard(true);
    }

    // 가드 아이콘 전용 핸들러
    private void OnGuardStartUI()
    {
        GuardIcon.SetState(UIGuard.GuardState.Active);
    }

    private void OnGuardEndUI()
    {
        // Player의 status에서 쿨타임 시간을 가져와서 코루틴 시작
        float cooldownTime = Player.Instance.status.GuardCooldownTime;
        GuardIcon.GuardCooldown(cooldownTime);
    }

    private void OnPerfectGuardUI(Vector2 pos, Vector2 dir)
    {
        GuardIcon.PlayPerfect();
    }
    #endregion

    #region UI Panel
    public void SetPausePanel(bool active)
    {
        pause?.gameObject.SetActive(active);
    }

    public void SetClearPanel(bool active)
    {
        clear?.gameObject.SetActive(active);
    }
    #endregion

    public void ShowNotice(string message)
    {
        if (noticeText != null) noticeText.Show(message);
    }

    #region Fader
    public void FadeIn(float duration = -1f, Action onComplete = null)
    {
        if(fader == null)
        {
            onComplete?.Invoke();
            return;
        }

        fader.FadeIn(duration, onComplete);
    }

    public void FadeOut(float durtaion = -1f, Action onComplete = null)
    {
        if (fader == null)
        {
            onComplete?.Invoke();
            return;
        }

        fader.FadeOut(durtaion, onComplete);
    }

    public void FadeInOut(float fadeInDuration = -1f, float holdTime = -1f, float fadeOutDuration = -1f, Action onMidComplete = null, Action onComplete = null, Color? color = null)
    {
        if(fader == null)
        {
            onMidComplete?.Invoke();
            onComplete?.Invoke();
            return;
        }

        if (color.HasValue) fader.SetFadeColor(color.Value);
        fader.FadeInOut(fadeInDuration, holdTime, fadeOutDuration, onMidComplete, onComplete);
    }
    #endregion
}
