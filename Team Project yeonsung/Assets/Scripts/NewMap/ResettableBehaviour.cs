using System.Collections.Generic;
using UnityEngine;

// 체크포인트에서 상태를 저장하고, 죽었다가 부활하면 저장한 상태로 되돌아가는 것들의 공통 부모.
// (깬 백혈구 벽·바닥, 먹은 항체, 더블점프 등. 나노 캡슐은 여기 넣지 않음 = 영구)
// GameObject를 끄지 말고 콜라이더·그림만 끄고 켜야 목록에서 빠지지 않습니다.
public abstract class ResettableBehaviour : MonoBehaviour
{
    static readonly List<ResettableBehaviour> all = new List<ResettableBehaviour>();
    public static IReadOnlyList<ResettableBehaviour> All { get { return all; } }

    object saved;
    bool hasSaved;

    protected virtual void Awake()
    {
        if (!all.Contains(this)) all.Add(this);
    }

    protected virtual void OnDestroy()
    {
        all.Remove(this);
    }

    public void SaveState()
    {
        saved = CaptureState();
        hasSaved = true;
    }

    public void RestoreState()
    {
        if (hasSaved) ApplyState(saved);
    }

    protected abstract object CaptureState();
    protected abstract void ApplyState(object state);
}
