using UnityEngine;

// 문 근처에 플레이어가 오면 안내 문구를 보여줍니다. (예: "감염 핵을 제거하고 오자.")
// 문 오브젝트가 꺼지면(InfectionHeart가 문을 열면) 문구도 같이 사라집니다.
[RequireComponent(typeof(Collider2D))]
public class DoorHint : MonoBehaviour
{
    public GameObject hint;     // 보여줄 문구 오브젝트

    void Start()
    {
        GetComponent<Collider2D>().isTrigger = true;
        if (hint != null)
        {
            hint.SetActive(false);
        }
    }

    void OnTriggerEnter2D(Collider2D other)
    {
        if (hint != null && other.GetComponentInParent<PlayerController>() != null)
        {
            hint.SetActive(true);
        }
    }

    void OnTriggerExit2D(Collider2D other)
    {
        if (hint != null && other.GetComponentInParent<PlayerController>() != null)
        {
            hint.SetActive(false);
        }
    }
}
