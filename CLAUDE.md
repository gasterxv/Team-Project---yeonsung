# CLAUDE.md

설명은 **한국어로, 쉬운 말로** 해 주세요. (작업자: 민재, 기획 담당 · Unity 입문)

## 세션 시작
- 세션을 시작하면 **가장 먼저 레포 루트의 `HANDOFF.md`를 읽기.** 지금 방향, 남은 작업, 답 못 받은 질문이 다 거기 있음.
- 그다음 `git status`, 현재 브랜치(`minjae-work`) 확인.

## "마무리"라고 하면
1. `HANDOFF.md` 갱신 — 이번 세션에 한 일, 바뀐 것, 남은 작업, 새로 생긴 질문, 맨 위 "마지막 갱신" 날짜
2. 커밋 (IDE 자동 생성 파일 `*.slnx` 등은 빼기)
3. `minjae-work` 브랜치에 푸시

그 외에는 사용자가 하라고 할 때만 커밋·푸시.

## 씬 수정 규칙
- **내 담당 씬은 자유롭게 수정** (끝나고 뭘 바꿨는지만 알려주기)
  - `Title.unity`, `메인씬 테스트.unity`, `MainScene_Rework.unity`, `Title_Layout.unity`, `Tutorial_Circulation.unity`
- **다른 팀원 씬은 수정 전에 무엇을 어떻게 바꿀지 먼저 확인받기, 그리고 삭제하지 않기** (씬·오브젝트·스크립트 전부. 필요하면 끄기만)
  - `ResultScene.unity`(pougom1005), `PlayerScenes`, `EnemyScenes`, `ItemScenes`, `ObjectScenes`
  - `#MainScenes#.unity`는 팀 공용 씬 → 팀원이 넣은 오브젝트(`설계도 지형`, 스폰, 체크포인트 등)는 같은 규칙 적용
- 다른 팀원의 이미지·스크립트 파일도 같은 규칙 (Import 설정 포함). 컴포넌트로 붙여 쓰는 건 OK.
- 어느 씬이 누구 것인지 애매하면 물어보기.

## 그 밖의 규칙 (자세한 건 HANDOFF.md 2장)
- 모르거나 애매하면 추측하지 말고 물어보기. 확인 못 한 건 "확인 못 했다"고 말하기.
- 맵을 고치기 전에 그림(미리보기)으로 먼저 보여주고 승인받기.
- 사용자가 "대화만 하자"고 하면 파일 수정 금지.
- Tools 메뉴로 새 씬 만들지 않기. `LayoutBuilder.cs`, `CirculatoryMapBuilder.cs`는 쓰지 않기.
- 클라우드 세션에서는 Unity 실행·플레이 확인이 안 됨 → 확인은 사용자에게 요청.
