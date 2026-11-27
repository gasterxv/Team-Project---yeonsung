using System.Collections;
using System.Collections.Generic;
using UnityEngine;

// 매복 구역 (함정 2 B). 플레이어가 이 트리거(방 바닥)에 들어오면 잠깐 바닥에 보라 얼룩이 생기고 몬스터가 튀어나옴. (글자 알림 없음)
// 보상은 없음. 한 번 나온 상태는 체크포인트에 저장되고, 저장 전에 죽으면 다시 매복 전으로 돌아감.
[RequireComponent(typeof(BoxCollider2D))]
public class AmbushZone : ResettableBehaviour
{
    public MonsterSpot[] spots;
    [Tooltip("얼룩이 생기고 몬스터가 나올 때까지 (초)")]
    public float warnTime = 0.9f;
    public float floorY = -22.6f;

    bool triggered;
    readonly List<SpawnWarningBlob> blobs = new List<SpawnWarningBlob>();

    protected override void Awake()
    {
        base.Awake();
        GetComponent<BoxCollider2D>().isTrigger = true;
    }

    void OnTriggerEnter2D(Collider2D other)
    {
        if (triggered || PlayerStateReader.FindPlayer(other) == null) return;
        triggered = true;
        StartCoroutine(Ambush());
    }

    IEnumerator Ambush()
    {
        foreach (MonsterSpot s in spots)
            if (s != null) blobs.Add(SpawnWarningBlob.Create(new Vector2(s.transform.position.x, floorY), warnTime));
        yield return new WaitForSeconds(warnTime);
        for (int i = 0; i < spots.Length; i++)
        {
            if (i < blobs.Count && blobs[i] != null) blobs[i].Pop();
            if (spots[i] != null) spots[i].Activate();
            yield return new WaitForSeconds(0.15f);
        }
        blobs.Clear();
        Debug.Log("[새맵] 함정 2 B 매복: 몬스터 " + spots.Length + "마리");
    }

    protected override object CaptureState() { return triggered; }

    protected override void ApplyState(object state)
    {
        StopAllCoroutines();
        foreach (SpawnWarningBlob b in blobs) if (b != null) Destroy(b.gameObject);
        blobs.Clear();
        triggered = (bool)state;
    }
}
