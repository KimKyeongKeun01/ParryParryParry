using UnityEngine;
using UnityEngine.UI;

public class UITitle : MonoBehaviour
{
    [SerializeField] private Button startBtn;
    [SerializeField] private Button quitBtn;


    public void Init()
    {
        // 기존 리스너 제거 (중복 방지)
        startBtn.onClick.RemoveAllListeners();
        quitBtn.onClick.RemoveAllListeners();

        // 새 리스너 등록
        startBtn.onClick.AddListener(() => GameManager.Instance.GameStart());
        quitBtn.onClick.AddListener(() => GameManager.Instance.GameQuit());
    }

    private void OnDestroy()
    {
        startBtn.onClick.RemoveAllListeners();
        quitBtn.onClick.RemoveAllListeners();
    }
}
