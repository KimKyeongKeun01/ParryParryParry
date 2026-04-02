using UnityEngine;
using UnityEngine.SceneManagement;

public class GameManager : MonoBehaviour
{
    private static GameManager instance;
    public static GameManager Instance
    {
        get { return instance; }
        private set { instance = value; }
    }
    public bool isPlaying = true;

    [Header("Game Scene")]
    [Tooltip("메인 메뉴 씬 이름")][SerializeField] private string mainSceneName;
    [Tooltip("게임 시작 씬 이름")][SerializeField] private string gameSceneName;

    void Awake()
    {
        if (instance == null)
        {
            instance = this;
            DontDestroyOnLoad(gameObject);
        }
        else
        {
            Destroy(gameObject);
        }
        Application.targetFrameRate = 60;
    }

    private void Start()
    {
        // Title 씬 세팅
        if (SceneManager.GetActiveScene().buildIndex == 0)
        {
            UIManager.Instance.title.gameObject.SetActive(true);
            UIManager.Instance.SetHealthBar(false);
            UIManager.Instance.SetPausePanel(false);
        }
        else
        {
            UIManager.Instance.title.gameObject.SetActive(false);
            UIManager.Instance.SetPausePanel(false);

            UIManager.Instance.UpdateHealth();
        }
    }

    private void Update()
    {
        if (Input.GetKeyDown(KeyCode.Escape) && !(SceneManager.GetActiveScene().buildIndex == 0))
        {
            if (isPlaying) GamePause();
            else GameResume();
        }
    }

    #region 게임 상태
    public void GameMain()
    {
        Time.timeScale = 1f;
        UIManager.Instance.FadeOut(onComplete: () =>
        {
            LoadScene(mainSceneName, true);
        });
    }

    public void GameStart()
    {
        UIManager.Instance.FadeOut(onComplete: () =>
        {
            // 기존 구독 해제 후 새 람다식 등록
            LoadManager.OnComplete -= () => UIManager.Instance.UpdateHealth(true);
            LoadManager.OnComplete += () => UIManager.Instance.UpdateHealth(true);

            LoadScene(gameSceneName, false);
        });
    }

    public void GamePause()
    {
        isPlaying = false;
        Time.timeScale = 0f;
        UIManager.Instance?.SetPausePanel(true);
    }

    public void GameRestart()
    {
        isPlaying = true;
        Time.timeScale = 1f; // 시간 초기화 필수!

        // 페이드 아웃 후 현재 씬 다시 로드
        UIManager.Instance.FadeOut(onComplete: () =>
        {
            string currentScene = UnityEngine.SceneManagement.SceneManager.GetActiveScene().name;
            LoadManager.Instance.LoadScene(currentScene, () =>
            {
                UIManager.Instance.SetPausePanel(false);
                UIManager.Instance.UpdateHealth();
                UIManager.Instance.FadeIn();

            });
        });
    }

    public void GameResume()
    {
        isPlaying = true;
        Time.timeScale = 1f;
        UIManager.Instance?.SetPausePanel(false);
    }

    public void GameClear()
    {
        isPlaying = false;
        Time.timeScale = 0f;
        Player.Instance.isPlaying = false;
        UIManager.Instance.SetClearPanel(true);
    }

    public void OnGameOver()
    {
        isPlaying = false;
        var player = Player.Instance;
        player.isPlaying = false;
        player.controller.Setup();

        UIManager.Instance.FadeOut(onComplete: () =>
        {
            var respawnPos = StageManager.Instance.GetRespawnPoint(); // 리셋 + Activate 포함
            player.Setup(respawnPos);

            UIManager.Instance.UpdateHealth(true);

            player.isPlaying = true;
            isPlaying = true;

            //CameraManager.Instance.SnapCurrentCamera();
            Time.timeScale = 1.0f;

            UIManager.Instance.FadeIn();
        });
        
    }

    public void GameQuit()
    {
#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#else
        Application.Quit();
#endif
    }

    private void LoadScene(string sceneName, bool main)
    {
        LoadManager.Instance.LoadScene(sceneName, () => {
            UIManager.Instance.FadeIn();
            UIManager.Instance.SetPausePanel(false);
            UIManager.Instance.SetClearPanel(false);
            UIManager.Instance.title.gameObject.SetActive(main);

            UIManager.Instance.SetGuard(!main);
        });
    }
    #endregion
}