using UnityEngine;
using UnityEngine.UI;


public class UICutScene : MonoBehaviour
{
    [Header("Letter Box")]
    [SerializeField] private Image topBox;
    [SerializeField] private Image bottomBox;
    
    [Header("Cutscene Settings")]
    [Tooltip("레터박스 차오르는 시간"), SerializeField] private float letterboxDuration;
    [Tooltip("레터박스 차오르는 높이"), SerializeField] private float targetLeight;

    public void PlayLetterboxIn()
    {
        // 레터박스 차오르는 연출
    }

    public void PlayLetterboxOut()
    {
        // 레터박스 사라지는 연출
    }
}