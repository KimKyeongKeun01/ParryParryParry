using UnityEngine;

public class BossVisual_Utan_mk2 : BaseBossVisual_mk2
{
    public override void PlayPhaseChange()
    {
        // 1. 애니메이션 트리거
        PlayAnim("OnPhaseChange");

        // 2. 색상 변경 (Base 기능 호출)
        OnPhaseChange(2, Color.red);

        // 3. 파티클 등 추가 연출
        //if (phaseChangeEffect != null) phaseChangeEffect.Play();

        Debug.Log("[Visual] Utan Phase 2 Enraged!");
    }
}
