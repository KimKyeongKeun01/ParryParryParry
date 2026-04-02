using UnityEngine;
using UnityEngine.UI;

public class UIPause : MonoBehaviour
{
    [SerializeField] private Button resumeBtn;
    [SerializeField] private Button reStartBtn;
    [SerializeField] private Button menuBtn;

    public void Init()
    {
        // 기존 리스너 제거 (중복 방지)
        resumeBtn.onClick.RemoveAllListeners();
        reStartBtn.onClick.RemoveAllListeners();
        menuBtn.onClick.RemoveAllListeners();

        // 새 리스너 등록
        resumeBtn.onClick.AddListener(() => GameManager.Instance.GameResume());
        reStartBtn.onClick.AddListener(() => GameManager.Instance.GameRestart());
        menuBtn.onClick.AddListener(() => GameManager.Instance.GameMain());
    }

    private void OnDestroy()
    {
        resumeBtn.onClick.RemoveAllListeners();
        reStartBtn.onClick.RemoveAllListeners();
        menuBtn.onClick.RemoveAllListeners();
    }
}
