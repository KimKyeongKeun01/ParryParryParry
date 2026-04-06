using Unity.VisualScripting;
using UnityEditor.Build;
using UnityEngine;
using UnityEngine.UIElements;

public class BossVisual_Met : BaseBossVisual
{
    [Header(" === Visual === ")]
    [SerializeField] private SpriteRenderer[] _hornSprites;

    public override void Init(BaseBoss _boss)
    {
        boss = _boss;
        if (_anim == null) _anim = GetComponentInChildren<Animator>();

        foreach (var s in _sprites)
        {
            s.color = originColor;
            originalScaleX = gameObject.transform.localScale.x;
        }

        Debug.Log($"[Boss Visual] {gameObject.name} Init Complete");
    }

    public override void PlayPhaseChange()
    {
        // 1. 애니메이션 트리거
        PlayAnim("OnPhaseChange");

        Color color;
        // #FFFFFF 형식의 Hex 코드를 컬러로 변환
        UnityEngine.ColorUtility.TryParseHtmlString("#1B011D", out color);

        // 2. 색상 변경 (Base 기능 호출)
        OnPhaseChange(2, color);

        // 3. 파티클 등 추가 연출
        //if (phaseChangeEffect != null) phaseChangeEffect.Play();

        Debug.Log("[Visual] Utan Phase 2 Enraged!");
    }
}
