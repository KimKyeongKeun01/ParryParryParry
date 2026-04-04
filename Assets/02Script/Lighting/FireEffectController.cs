using UnityEngine;
using UnityEngine.Rendering.Universal; // 2D 조명을 다루기 위한 마법의 주문입니다!

public class FireEffectController : MonoBehaviour
{
    [Header("애니메이션 연동")]
    // 애니메이션 창에서 0 -> 1로 스르륵 올릴 변수입니다.
    public float fireSize = 0f; 

    [Header("조명 설정")]
    public Light2D fireLight; // 하이어라키에서 Light 2D를 끌어다 넣을 칸
    public float maxIntensity = 3f; // 불이 최대로 커졌을 때의 밝기

    [Header("일렁임(Sine) 설정")]
    public float flutterSpeed = 10f; // 파르르 떨리는 속도 (노드에서 곱했던 Time Speed)
    public float flutterAmount = 0.02f; // 떨리는 진폭 (노드에서 곱했던 0.02)

    void Update()
    {
        // 1. 일렁임(Sine 파동) 계산 
        // 노드 로직: Mathf.Sin(Time * Speed) * 0.02
        float flutter = Mathf.Sin(Time.time * flutterSpeed) * flutterAmount;

        // 2. 최종 크기 계산: (기본 크기 1 + 일렁임) * 애니메이션 사이즈(0~1)
        float currentScale = (1f + flutter) * fireSize;
        
        // Transform에 적용 (Float을 Vector3로 깔끔하게 묶어서 넣습니다)
        transform.localScale = new Vector3(currentScale, currentScale, currentScale);

        // 3. 조명 밝기 적용: (최대 밝기 * 애니메이션 사이즈)
        if (fireLight != null)
        {
            fireLight.intensity = maxIntensity * fireSize;
        }
    }
}