using UnityEngine;

// 노란 문(감염된 문). 감염핵을 부수면 InfectionHeart가 이 오브젝트를 꺼서 문이 열림.
// 열린 상태는 체크포인트에 저장되고, 저장 전에 죽으면 다시 닫힘(켜짐).
// 충돌 크기 0.8 × 4 (통로 높이 4를 딱 막음), 그림은 조금 더 크게 (벽에 박힌 느낌). 그림은 Sprite만 바꾸면 됨.
public class InfectedDoor : ResettableBehaviour
{
    protected override object CaptureState() { return !gameObject.activeSelf; }   // true = 열림

    protected override void ApplyState(object state)
    {
        bool open = (bool)state;
        gameObject.SetActive(!open);
    }
}
