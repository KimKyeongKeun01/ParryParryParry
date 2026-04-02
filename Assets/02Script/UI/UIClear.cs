using UnityEngine;
using UnityEngine.UI;

public class UIClear : MonoBehaviour
{
    [SerializeField] private Button quitBtn;

    public void Init()
    {
        // 기존 리스너 제거 (중복 방지)
        quitBtn.onClick.RemoveAllListeners();

        // 새 리스너 등록
        quitBtn.onClick.AddListener(() => GameManager.Instance.GameQuit());
    }

    private void OnDestroy()
    {
        quitBtn.onClick.RemoveAllListeners();
    }
}
