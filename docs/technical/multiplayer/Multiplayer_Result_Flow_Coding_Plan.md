# 🏁 Multiplayer Result Flow Coding Plan

이 문서는 multiplayer 전투 결과 패널(`Victory` / `Defeated`) 구현을 시작하기 전에
current runtime logic, fix direction, coding steps를 고정하기 위한 계획서다.

이 문서는 UI 레이아웃 문서가 아니라,
`Host writes gameplay truth, all peers show the same result panel` contract를
안전하게 코드에 반영하기 위한 implementation baseline이다.

2026-04-07 same-day follow-up에서는 initial result flow 구현 뒤 verify blocker가 남아,
`result panel image contract`와 `dead-player spectator follow(position-only)`까지
scope extension으로 같이 반영했다.

---

## 1. 문서 목적

이 문서는 아래 3가지를 명확히 남긴다.

* current result logic이 지금 어떻게 동작하는가
* multiplayer에서 왜 panel popup이 틀리거나 빠지는가
* 어떤 순서로 고쳐야 solo path를 깨지 않고 multiplayer result flow를 붙일 수 있는가

---

## 2. 기준 문서와 범위

| 항목 | 내용 |
| --- | --- |
| 상위 기준 문서 | `docs/technical/multiplayer/Multiplayer_Design.md` |
| 관련 UI 문서 | `docs/technical/multiplayer/Multiplayer_UI_Flow.md` |
| 관련 boss authority 문서 | `docs/technical/multiplayer/boss/Multiplayer_Boss_Authority.md` |
| 이번 문서 범위 | gameplay result panel popup logic |
| 이번 문서 비포함 | title return sync polish |

메모:

* `Multiplayer_UI_Flow.md`는 pre-game panel flow를 다루고, result UI detail은 범위 밖이다.
* 그래서 이번 항목은 별도 coding plan 문서로 분리한다.

---

## 3. 목표

* boss HP가 `0`이 되면 Host와 Client 양쪽 화면에 `Victory` panel이 뜨게 한다.
* player 한 명만 죽었을 때는 game over를 띄우지 않고 전투를 계속 유지한다.
* 두 player가 모두 죽었을 때만 Host와 Client 양쪽 화면에 `Defeated` panel이 뜨게 한다.
* 패배 panel 하단에는 `Press Enter to Play (0/2)` prompt를 표시한다.
* 두 player가 모두 `Enter`를 누르면 새 게임을 다시 시작한다.
* solo play result logic은 current behavior를 유지한다.

---

## 4. current result logic

### 4.1. current runtime summary

current codebase는 `GameManager`가 result UI를 직접 관리한다.

* `GameManager`는 `_playerHealth`와 `_bossHealth`를 들고 있다.
* `OnDeath` event를 받아 `_playerDead`, `_bossDead` bool을 갱신한다.
* `LateUpdate()`에서 아래 규칙으로 result를 확정한다.

```text
if boss dead -> Victory
else if player dead -> Defeated
```

이 구조는 solo play에서는 단순하고 잘 맞는다.
하지만 multiplayer에서는 이 로직이 아직 `single player + local boss health` 기준에 머물러 있다.

### 4.2. code path summary

| 시스템 | current 역할 |
| --- | --- |
| `GameManager` | result panel 표시와 restart input 처리 |
| `MultiplayerPlayerAvatar` | local owner HUD HP snapshot 반영, local player bind |
| `MultiplayerBossAuthorityBridge` | boss authoritative state와 boss HUD HP 반영 |
| `Health` | local runtime HP source |

### 4.3. multiplayer에서 어긋나는 지점

#### A. defeat rule is still single-player based

`GameManager`는 current scene에서 단 하나의 `_playerHealth`만 본다.
그래서 multiplayer에서 local bound player가 죽으면,
current logic은 `both dead`를 보기 전에 먼저 `Defeated` 조건으로 떨어질 수 있다.

#### B. boss result source and boss HUD source are different

boss HP bar는 이미 `MultiplayerBossAuthorityBridge`의 authoritative snapshot을 읽는다.
하지만 `GameManager` result check는 여전히 local `_bossHealth.OnDeath`에 묶여 있다.

즉, multiplayer boss HUD truth와 result truth가 아직 같은 source를 쓰지 않는다.

#### C. legacy scene player handoff 이후에도 result logic은 old solo shape다

multiplayer gameplay startup에서는 old scene player가 제거되고
spawned `MultiplayerPlayerAvatar` player object가 runtime owner가 된다.

하지만 result logic은 아직 `one player health reference` shape를 유지한다.

### 4.4. current behavior vs target behavior

| 상황 | current logic | target logic |
| --- | --- | --- |
| boss HP = 0 | local `_bossHealth` death에만 의존 | authoritative boss dead state로 Victory |
| player A dead, player B alive | tracked local player가 죽으면 Defeated 위험 | no result panel |
| player A dead, player B dead | single player death와 구분이 약함 | both dead일 때만 Defeated |
| Host/Client panel sync | local source mismatch 가능 | both peers show same result |

---

## 5. 6-Line Trace Card

1. `[S1] Trigger | Boss HP or player HP changes | Assets/Scripts/Common/Combat/Health.cs | OnDeath / CurrentHealth`
2. `[S2] Entry | Result flow starts in GameManager | Assets/Scripts/Common/GameManager.cs | BindHealthEvents / LateUpdate`
3. `[S3] Gate | Current result gate reads one _playerHealth and one _bossHealth | Assets/Scripts/Common/GameManager.cs | _playerDead / _bossDead`
4. `[S4] Core Check | Current rule is boss dead => Victory else player dead => Defeated | Assets/Scripts/Common/GameManager.cs | single-player assumption`
5. `[S5] Effect | Multiplayer player HP and boss HP already use separate authority bridges for HUD | Assets/Scripts/Multiplayer/Gameplay/MultiplayerPlayerAvatar.cs / Assets/Scripts/Multiplayer/Gameplay/MultiplayerBossAuthorityBridge.cs | result path not joined yet`
6. `[S6] Result | Multiplayer result panel can miss boss victory or fire defeat too early | Assets/Scripts/Common/GameManager.cs | panel popup mismatch`

---

## 6. how to fix it

### 6.1. chosen direction

이번 수정은 new multiplayer-only result manager를 크게 새로 만드는 방향보다,
existing `GameManager`를 유지하면서 multiplayer result source만 authority-aware하게 바꾸는 방향을 선택한다.

이 방향의 핵심은 아래와 같다.

* solo path는 current local `Health` event flow를 유지한다.
* multiplayer path는 `local Health` 대신 existing authoritative multiplayer surfaces를 읽는다.
* result rule은 `Victory first, then both-dead Defeated` 순서로 고정한다.

### 6.2. victory rule

`Victory`는 boss authoritative state를 기준으로 판정한다.

쉬운 영어로 쓰면:

* If authoritative boss state says dead, show `Victory`.
* Do not wait for local disabled boss `Health` on the client.

실행 source 후보:

* `MultiplayerBossAuthorityBridge`가 latest boss state를 read-only로 노출한다.
* `GameManager`는 multiplayer session일 때 그 값을 읽는다.

### 6.3. defeated rule

`Defeated`는 local player one-body death가 아니라,
spawned multiplayer avatar 둘 다 dead인지로 판정한다.

쉬운 영어로 쓰면:

* Count all active player avatars.
* Count how many of them are dead.
* Show `Defeated` only when all active avatars are dead.

현재 범위에서는 `2 players only`가 locked rule이므로,
실질 판정은 `deadCount >= 2` 또는 `deadCount == avatarCount == 2`로 읽을 수 있다.

패배 panel text contract는 아래를 사용한다.

```text
Defeated
Press Enter to Play (0/2)
```

그 다음 ready count는 same defeat panel 하단 text에서 아래처럼 올라간다.

```text
Press Enter to Play (1/2)
Press Enter to Play (2/2)
```

쉬운 영어로 쓰면:

* When both players are dead, show `Defeated`.
* Show `Press Enter to Play (0/2)` under it.
* If one player presses `Enter`, update it to `(1/2)`.
* If both players press `Enter`, start a new game.

### 6.4. source contract

| result type | source | reason |
| --- | --- | --- |
| `Victory` | `MultiplayerBossAuthorityBridge` latest authoritative boss dead state | boss HUD truth와 result truth를 맞춘다 |
| `Defeated` | `MultiplayerPlayerAvatar` replicated player HP/dead state aggregate | one local player death와 both-dead를 분리한다 |
| `Retry Ready Count` | Host-owned ready state by player | both peers가 같은 `(0/2 -> 1/2 -> 2/2)` count를 보게 한다 |

### 6.5. scope boundary

이번 범위에서 하지 않는 것:

* title return network UX polish

이번 범위에서 먼저 확정하는 것:

* result panel popup 조건
* Host/Client same result visibility
* defeat 상태에서 `Enter` 2인 합의 후 restart
* dead local player spectator follow(position-only, keep local orbit input, about `2.5s` delay)

---

## 7. coding steps

### Step 1. expose read-only multiplayer result surfaces

목표:

* `GameManager`가 multiplayer authority truth를 읽을 수 있게 한다.

핵심 작업:

* `MultiplayerBossAuthorityBridge`에 latest authoritative boss result read surface를 추가한다.
* `MultiplayerPlayerAvatar`에 replicated HP/dead read surface를 추가한다.

예상 helper 예시:

* `bool HasLatestBossState`
* `bool IsBossDead`
* `bool TryGetReplicatedHealth(out int currentHealth, out int maxHealth)`
* `bool IsReplicatedDead`

완료 조건:

* `GameManager`가 result 판정을 위해 multiplayer runtime object를 polling할 수 있다.

### Step 2. split GameManager result path into solo and multiplayer

목표:

* `GameManager`가 session mode에 따라 다른 source를 읽게 한다.

핵심 작업:

* current solo `BindHealthEvents()` / `_playerDead` / `_bossDead` path는 유지한다.
* multiplayer session이면 `LateUpdate()` 또는 dedicated resolver에서 authoritative surfaces를 읽는다.
* result resolution order를 아래처럼 고정한다.

```text
if multiplayer:
    if boss authoritative dead -> Victory
    else if both players dead -> Defeated
else:
    keep current solo logic
```

완료 조건:

* solo behavior는 유지되고, multiplayer result는 new authority source를 읽는다.

### Step 3. resolve both-player death correctly

목표:

* `one dead != game over` rule을 코드에 고정한다.

핵심 작업:

* active `MultiplayerPlayerAvatar`를 수집한다.
* 각 avatar의 replicated HP/dead state를 읽는다.
* alive avatar가 하나라도 남아 있으면 result를 띄우지 않는다.

완료 조건:

* one player dead일 때 panel이 안 뜬다.
* both players dead일 때만 `Defeated`가 뜬다.
* `Defeated` panel 하단에 `Press Enter to Play (0/2)`가 뜬다.

### Step 4. add defeat retry ready-count flow

목표:

* 패배 후 두 player가 `Enter`를 눌러야 새 게임이 시작되게 한다.

핵심 작업:

* `GameManager` 또는 same result owner가 multiplayer defeat state에서 ready count를 가진다.
* 각 local player의 `Enter` input을 Host authority로 기록한다.
* Host는 ready count를 `0/2 -> 1/2 -> 2/2`로 관리한다.
* 양쪽 화면은 같은 defeat prompt count를 본다.
* `2/2`가 되면 Host가 새 게임 시작을 트리거한다.

완료 조건:

* 첫 번째 `Enter` 뒤에는 `Press Enter to Play (1/2)`가 보인다.
* 두 번째 `Enter` 뒤에는 `Press Enter to Play (2/2)`가 보이고, 새 게임이 시작된다.

### Step 5. resolve boss victory correctly on both peers

목표:

* boss death popup이 Host와 Client에 같은 source로 뜨게 한다.

핵심 작업:

* Host는 boss `Health`와 boss authoritative state가 서로 같은 dead state를 유지한다.
* Client는 local disabled boss `Health`가 아니라 bridge latest state만 믿는다.
* same frame duplicate resolve는 `_isGameOverResolved` gate로 막는다.

완료 조건:

* boss HP가 `0`이 되면 양쪽 화면에 `Victory`가 뜬다.

### Step 6. keep result UI presentation aligned with the new loss rule

목표:

* 이번 작업은 existing result panel을 유지하면서 defeat prompt text contract를 맞춘다.

핵심 작업:

* `_gameOverRoot`, `_resultLabel`, trigger animation, current texts는 최대한 유지한다.
* verify scene/prefab contract에서는 `Image_Win`, `Image_Lose`, `Text_GameResult` root도 같이 토글해야 한다.
* defeat 상태에서는 아래 panel contract를 사용한다.

```text
Image_Lose
Press Enter to Play (x/2)
```

* victory 상태에서는 `Image_Win` art를 보여 주고, extra text가 필요 없으면 빈 text도 허용한다.
* UI prefab/layout 변경 없이 existing image/text object toggle path로 처리한다.

완료 조건:

* existing panel asset과 animator를 그대로 사용한다.
* `Victory`는 `Image_Win`, `Defeated`는 `Image_Lose + retry prompt`로 보인다.

### Step 7. verify gameplay scenarios

목표:

* user-visible result rule이 정확히 맞는지 확인한다.

수동 검증 시나리오:

1. Host 화면에서 boss HP를 `0`으로 만들면 `Victory` panel이 뜬다.
2. Client 화면에서도 same boss death에 `Victory` panel이 뜬다.
3. player A만 죽으면 어느 쪽 화면에도 `Defeated` panel이 뜨지 않는다.
4. player B도 죽으면 Host/Client 양쪽에 `Defeated`와 `Press Enter to Play (0/2)`가 뜬다.
5. 첫 번째 player가 `Enter`를 누르면 양쪽 화면 prompt가 `Press Enter to Play (1/2)`로 바뀐다.
6. 두 번째 player도 `Enter`를 누르면 양쪽 화면 prompt가 `Press Enter to Play (2/2)`를 거쳐 새 게임이 시작된다.
7. solo play에서는 기존 result flow가 그대로 유지된다.

### Step 8. dead-player spectator follow (same-day scope extension)

목표:

* dead local player가 alive partner를 볼 수 있게 하되, alive player camera ownership은 건드리지 않는다.

핵심 작업:

* `ThirdPersonCameraController`는 multiplayer gameplay에서 local avatar dead 여부를 읽는다.
* dead local player는 local death edge 뒤 약 `2.5초`를 기다린다.
* alive partner가 있으면, local orbit/look input은 그대로 유지한 채 follow position만 partner 쪽으로 바꾼다.
* alive player의 exact camera나 local look state를 복사하지 않는다.

완료 조건:

* one dead / one alive 상태에서 dead player 화면이 약 `2.5초` 뒤 alive partner를 따라간다.
* alive player의 own camera/input은 그대로 유지된다.

---

## 8. 영향 파일

### 8.1. 수정 파일 (예상)

* `Assets/Scripts/Common/GameManager.cs`
* `Assets/Scripts/Multiplayer/Gameplay/MultiplayerPlayerAvatar.cs`
* `Assets/Scripts/Multiplayer/Gameplay/MultiplayerBossAuthorityBridge.cs`

### 8.2. 문서 파일 (구현 후 동기화 대상)

* `docs/Progress_Log/YYYY-MM-DD.md`
* `docs/Progress_Log/README.md`
* `docs/technical/System_Blueprint.md`
* `docs/technical/Technical_Glossary.md`

필요 시 아래 문서도 same-day update 대상으로 본다.

* `docs/technical/multiplayer/Multiplayer_Design.md`
* `docs/technical/multiplayer/Multiplayer_Result_Flow_Coding_Plan.md`

---

## 9. 리스크

* `GameManager`가 solo path와 multiplayer path를 동시에 읽으면 duplicate resolve가 생길 수 있다.
* avatar count resolve 타이밍이 spawn/despawn과 엇갈리면 temporary false defeat가 날 수 있다.
* client result check가 local disabled boss `Health`로 fallback되면 boss victory가 다시 틀어질 수 있다.
* retry ready count를 local-only input으로 처리하면 Host/Client prompt count가 서로 달라질 수 있다.
* spectator follow가 alive player exact camera ownership까지 건드리면 partner local look state를 오염시킬 수 있다.

---

## 10. 구현 메모

현재 코드 방향과 가장 잘 맞는 원칙은 아래와 같다.

1. Keep `GameManager` as the panel owner.
2. Keep Host as the gameplay authority.
3. Read boss death from boss authority bridge.
4. Read player death from all spawned player avatars.
5. Resolve retry ready count on Host and mirror the same `(x/2)` text to both peers.
6. Keep result UI on existing `Image_Win` / `Image_Lose` / `Text_GameResult` contract.
7. Keep dead-player spectator as follow-position-only, not exact-camera ownership swap.

---

## 11. 승인 게이트

이 문서는 implementation 전에 사용하는 result-flow 계획서다.

* code implementation은 이 계획서 승인 후에 시작한다.
* 구현 중 scope가 바뀌면 먼저 이 문서를 갱신하고 재승인을 받는다.
* 2026-04-07 same-day follow-up에서 spectator camera는 position-only scope extension으로 반영했다.
