# 🧾 Study Log - 2026-02-21 (Kickoff)

## 기본 정보

- 날짜: `2026-02-21`
- 세션 유형: `주말 정리`
- 오늘 목표: `2/4~2/5 이후 플레이어 입력-상태 흐름 변경점 다시 잡기`

---

## 1) 오늘 추적한 경로

| 순서 | 파일 | 확인한 내용(한 줄) |
| --- | --- | --- |
| 1 | `Assets/Scripts/Player/PlayerInputData.cs` | 입력 버튼이 `InputFlag + byte buttons`로 비트 패킹됨 |
| 2 | `Assets/Scripts/Player/LocalInputProvider.cs` | Input System 값을 읽어 `PlayerInputPacket`으로 포장 |
| 3 | `Assets/Scripts/Player/PlayerController.cs` | 컨트롤러는 카메라 회전 + 현재 상태 `Update` 호출 중심 |
| 4 | `Assets/Scripts/Player/States/MoveState.cs` | 점프 전환은 현재 비활성화, 공격/대시 전환이 우선 |
| 5 | `Assets/Scripts/Player/States/AttackState.cs` | 콤보 예약, 대시 캔슬, 종료 시 Move 복귀 구조 |
| 6 | `Assets/Scripts/Common/Combat/Health.cs` | `OnDamageTaken`, `OnDeath` 이벤트로 상태 전환 트리거 |

---

## 2) 내가 이해한 로직 (핵심 3줄)

1. 입력은 `PlayerInputPacket`으로 표준화되어 컨트롤러와 상태 로직이 느슨하게 연결된다.
2. `PlayerController`는 "판단"보다 "연결/실행"에 집중하고, 실제 전이 조건은 각 상태 클래스가 가진다.
3. 공격 판정은 애니메이션 이벤트(`OnHitStart/OnHitEnd`)를 통해 `DamageCaster`가 활성화되는 구조다.

---

## 3) 막힌 지점 / 헷갈리는 조건

- `AttackState`에서 `_wasAttackPressed`를 `Enter` 시 true로 두는 이유를 프레임 단위로 다시 검증 필요.
- 현재 점프 비활성 정책(F10 유지)의 의도와 재활성 조건을 정리할 필요가 있음.

---

## 4) 다음 액션 (반드시 1개 이상)

- [ ] `AttackState.Update()`를 프레임 타임라인으로 그려서 `_reserveNextCombo`가 언제 켜지는지 기록한다.
- [ ] `PlayerVisual -> PlayerController -> DamageCaster` 호출 체인을 유니티에서 브레이크포인트로 재확인한다.

---

## 5) 면접 설명 문장 (2~3문장)

`이 프로젝트는 입력 수집과 게임 로직을 분리하기 위해 PlayerInputPacket 기반 파이프라인을 사용합니다. PlayerController는 상태 실행의 관제 역할만 맡고, 이동/대시/공격 판단은 상태 클래스에 위임해 유지보수성과 확장성을 확보했습니다.`

