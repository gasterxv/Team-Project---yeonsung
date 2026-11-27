using UnityEngine;

// 감염핵 자리. 팀원 InfectionHeart(부서지면 스스로 삭제)는 고치지 않고,
// 처음 모습을 숨겨 둔 복사본으로 보관해 두었다가, 체크포인트 전에 부쉈는데 플레이어가 죽으면 다시 만들어 줍니다.
// (InfectionHeart의 bloodVesselDoor = 노란 문 → 부수면 문이 꺼짐. 복사본도 같은 문을 가리킴)
public class HeartSpot : ResettableBehaviour
{
    public InfectionHeart heart;   // 씬에 놓인 감염핵

    GameObject template;           // 처음 모습 (꺼진 채 보관)

    protected override void Awake()
    {
        base.Awake();
        if (heart == null) return;
        GameObject holder = new GameObject("감염핵 보관 (꺼짐)");
        holder.transform.SetParent(transform, false);
        holder.SetActive(false);   // 꺼진 부모 아래에 복사 → 복사본의 Awake가 돌지 않음
        template = Instantiate(heart.gameObject, heart.transform.position, heart.transform.rotation, holder.transform);
        template.name = heart.gameObject.name;
    }

    bool Broken { get { return heart == null; } }

    protected override object CaptureState() { return Broken; }

    protected override void ApplyState(object state)
    {
        bool wasBroken = (bool)state;
        if (wasBroken)
        {
            if (heart != null) Destroy(heart.gameObject);
            heart = null;
            return;
        }
        if (heart != null) Destroy(heart.gameObject);   // 맞던 중이면 체력도 처음으로
        if (template == null) return;
        GameObject g = Instantiate(template, template.transform.position, template.transform.rotation, transform.parent);
        g.name = template.name;
        g.SetActive(true);
        heart = g.GetComponent<InfectionHeart>();
    }
}
