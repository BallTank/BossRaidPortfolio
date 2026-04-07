# 🐉 Multiplayer Boss Authority Coding Plan

이 문서는 `docs/technical/multiplayer/boss/Multiplayer_Boss_Authority.md` 기준으로
boss authority 구현을 시작하기 전에 사용하는 계획서다.

이 계획서는 특정 날짜의 임시 구현을 고정하는 문서가 아니라,
`Host writes gameplay truth, Client reads and presents`라는 long-term contract를
코드에 안전하게 반영하기 위한 implementation baseline이다.

---

## 1. 목표

* multiplayer boss를 `temporary presentation-only mirror`에서 분리하고,
  dedicated boss authority path로 정리한다.
* Host가 boss의 gameplay truth를 단일 소스로 가진다.
* Client는 boss gameplay simulation을 하지 않고,
  authoritative state를 받아 interpolation + animation playback + UI update만 수행한다.
* current boss authority milestone의 1차 범위를
  `move sync -> Basic sync -> Lunge sync -> boss HP/phase/death sync` 순서로 고정한다.

---

## 2. 범위

### 2.1. 포함 범위

* boss dedicated authority snapshot/state contract 추가
* Host send path와 Client apply path 분리
* boss movement sync
* boss `Basic` attack sync
* boss `Lunge` attack sync
* boss taking damage / HP / phase / death sync
* multiplayer boss HUD authoritative bind 정리
* temporary boss mirror cleanup 또는 compatibility off 경로 정리
* same-day scope extension: attack 3 projectile / attack 4 AoE visual remote replay

### 2.2. 제외 범위

* boss aggro rule 설계 변경
* projectile / AoE full sync
* spectator / retry consensus / result flow 확장
* boss AI pattern redesign
* visual polish only 튜닝 작업

메모:

* target selection은 current runtime rule을 유지한다.
* aggro는 이 계획서의 책임 범위가 아니다.

### 2.3. same-day scope extension (2026-04-06)

current verify 결과, Host 화면에서는 attack 3 projectile과 attack 4 red circle/falling fire가 이미 보였고,
빠진 것은 remote client의 spawned effect replay였다.

따라서 이번 확장은 `projectile / AoE full sync` 전체를 여는 것이 아니라,
아래 visual replication slice만 추가한다.

* Host가 attack 3 projectile spawn 시점을 effect event로 기록한다.
* Host가 attack 4 red circle / falling fire spawn 시점을 effect event로 기록한다.
* Client는 local pooled projectile / AoE object를 display-only로 재생한다.
* damage / hit / HP truth는 계속 Host authoritative path로 유지한다.

---

## 3. 코드 기준선

codebase는 아래 상태를 가진다.

* client local boss authority는 의도적으로 꺼져 있다.
* current multiplayer verify path는 `MultiplayerPlayerAvatar`가 boss presentation snapshot을 owner client로 보내는 temporary mirror를 사용한다.
* current mirror는 movement + current attack animation state display에는 유용하지만,
  boss authority 구현의 final structure로 두기에는 player code와 boss code가 섞인다.
* player 쪽은 이미 Host authoritative action/reaction/HP write gate path를 가지고 있으므로,
  boss authority 구현은 이 구조와 같은 방향으로 정리하는 것이 안전하다.

목표: 지정된 보스 권한 경로에 임시 player-avatar 경로의 boass 권한을 옮긴다.

---

## 4. 구현 방향 결정

### 4.1. 선택한 방향

이번 구현은 아래 방향을 기준으로 진행한다.

* `dedicated boss snapshot/state layer`를 만든다.
* Host가 boss move / attack / damage / phase / death truth를 기록한다.
* Client는 boss gameplay prediction을 하지 않는다.
* client boss는 interpolation + display-only apply만 수행한다.
* projectile/AoE는 later slice로 남기고, current milestone은 `move / Basic / Lunge / HP / phase / death`까지만 묶는다.

### 4.2. 구조 원칙

* solo boss runtime은 최대한 유지한다.
* multiplayer-specific send/apply responsibility는 boss bridge 쪽으로 분리한다.
* `MultiplayerPlayerAvatar`는 player authority owner로 남기고,
  boss authority owner 역할은 점진적으로 내려놓는다.
* current temporary mirror와 new dedicated path가 동시에 truth를 쓰지 않게,
  one packet / one path boundary를 먼저 고정한다.

---

## 5. 영향 파일

### 5.1. 신규 파일 (예상)

* `Assets/Scripts/Multiplayer/Gameplay/BossAuthoritativeState.cs`
  * boss authoritative snapshot DTO
* `Assets/Scripts/Multiplayer/Gameplay/BossAttackReplicationState.cs`
  * current attack id / start tick / phase / flags 정리용 DTO
* `Assets/Scripts/Multiplayer/Gameplay/MultiplayerBossAuthorityBridge.cs`
  * Host capture / send / Client apply를 담당하는 dedicated boss authority bridge

### 5.2. 수정 파일 (예상)

* `Assets/Scripts/Multiplayer/Gameplay/MultiplayerGameplaySceneCoordinator.cs`
  * scene startup 시 boss authority runtime 준비 및 client local disable 책임 정리
* `Assets/Scripts/Multiplayer/Gameplay/MultiplayerPlayerAvatar.cs`
  * current temporary boss mirror send/apply path 제거 또는 compatibility off
* `Assets/Scripts/Boss/BossController.cs`
  * boss authoritative state capture에 필요한 access point 정리
* `Assets/Scripts/Boss/BossFSM.cs`
  * move / attack boundary가 snapshot contract와 맞도록 최소 helper 정리
* `Assets/Scripts/Boss/Attacks/BasicAttackPattern.cs`
  * authoritative attack start timing export helper
* `Assets/Scripts/Boss/Attacks/LungeAttackPattern.cs`
  * authoritative lunge start/active/end timing export helper
* `Assets/Scripts/UI/CombatHUDController.cs`
  * multiplayer boss HP authoritative bind 필요 시 manual path 정리
* `Assets/Scripts/Player/PlayerController.cs`
  * boss hit / reaction / HUD bind가 new boss authority path와 충돌하지 않도록 최소 조정

### 5.3. 문서 파일 (구현 후 동기화 대상)

* `docs/Progress_Log/2026-04-06.md`
* `docs/Progress_Log/README.md`
* `docs/technical/System_Blueprint.md`
* `docs/technical/Technical_Glossary.md`
* `docs/technical/multiplayer/boss/Multiplayer_Boss_Authority.md`

---

## 6. 설계 근거

### 6.1. Authority Contract

기준 문서 `Multiplayer_Boss_Authority.md`는 아래 contract를 고정한다.

* Boss는 `Host-owned gameplay actor with replicated state`다.
* Host는 move / attack / damage / phase / death를 결정한다.
* Client는 presentation만 담당한다.
* replicated state는 minimal category를 유지하되 exact packet shape는 구현에서 조정 가능하다.

### 6.2. Existing Multiplayer Direction

`System_Blueprint`와 recent `Progress_Log` 기준으로,
current multiplayer direction은 `Host authority first, dedicated boundary later cleanup`이다.

같은 원칙을 boss에도 적용한다.

* 먼저 Host truth를 고정한다.
* 그 다음 send/apply boundary를 dedicated path로 분리한다.
* remote side는 gameplay runtime을 돌리지 않고 display-only path를 따른다.

### 6.3. Scope Boundary

이번 plan은 boss authority 문서에 맞춘 implementation plan이므로,
boss aggro나 projectile/AoE full sync까지 같이 열지 않는다.

즉, current slice는 아래만 처리한다.

* move
* Basic
* Lunge
* HP
* phase
* death

---

## 7. 단계별 구현 계획

### Step 1. Dedicated Boss State Contract

목표:

* boss authority send/apply가 player-avatar mirror에 의존하지 않도록,
  dedicated snapshot/state contract를 먼저 만든다.

핵심 작업:

* boss transform, locomotion state, current attack id, attack start tick/time, HP, phase, dead flag를 담는 struct를 정의한다.
* exact field shape는 small and explicit하게 유지한다.
* future projectile/AoE field는 지금 넣지 않는다.

완료 조건:

* Host가 boss current truth를 dedicated DTO로 capture할 수 있다.

### Step 2. Dedicated Boss Bridge

목표:

* boss send/apply responsibility를 `MultiplayerPlayerAvatar`에서 분리한다.

핵심 작업:

* Host-owned network bridge가 boss authoritative state를 capture해서 client로 보낸다.
* Client는 local boss reference를 찾아 display-only apply를 수행한다.
* client local boss logic / physics는 계속 disabled로 유지한다.

완료 조건:

* boss movement와 animator state apply가 dedicated bridge path를 통해 동작한다.
* old temporary mirror path는 truth writer에서 빠진다.

### Step 3. Boss Move Sync

목표:

* boss transform + locomotion state를 dedicated path로 안정화한다.

핵심 작업:

* Host는 boss runtime position / rotation / locomotion state를 authoritative하게 보낸다.
* Client는 interpolation 중심으로 visible movement를 재생한다.
* client-side gameplay prediction은 넣지 않는다.

완료 조건:

* Host와 Client가 same boss move/chase/idle direction을 본다.

### Step 4. Boss `Basic` Attack Sync

목표:

* `Basic` attack start와 timing을 Host authority로 재생한다.

핵심 작업:

* Host가 `Basic` attack start를 결정하고 attack id + start tick/time을 보낸다.
* Client는 same attack state를 display-only로 재생한다.
* damage result는 기존처럼 Host authoritative player reaction path로 남긴다.

완료 조건:

* Host와 Client 화면에서 `Basic` attack의 시작과 진행이 같은 contract를 따른다.
* client가 local boss hit truth를 직접 만들지 않는다.

### Step 5. Boss `Lunge` Attack Sync

목표:

* `Lunge` attack start / travel / land timing을 Host authority로 재생한다.

핵심 작업:

* Host가 `Lunge` start tick/time을 기록한다.
* Client는 authoritative transform + attack state를 기준으로 lunge visual을 재생한다.
* root motion visual drift를 줄이되 gameplay truth는 transform snapshot 쪽에 둔다.

완료 조건:

* Host와 Client가 `Lunge` 시작/이동/착지 리듬을 같은 contract로 본다.

### Step 6. Boss HP / Phase / Death Sync

목표:

* player attack이 boss에 들어간 결과를 Host authoritative boss state로 고정한다.

핵심 작업:

* Host가 boss HP 감소를 최종 기록한다.
* phase pending / phase switch / death start도 Host가 결정한다.
* Client는 boss HP bar, phase visual, death visual을 authoritative state로만 갱신한다.

완료 조건:

* boss HP, phase, death order가 Host/Client 두 화면에서 같게 보인다.

### Step 7. Cleanup and Verify

목표:

* temporary boss mirror dependency를 제거하고, current slice를 verify 가능한 상태로 닫는다.

핵심 작업:

* `MultiplayerPlayerAvatar`의 boss mirror path를 제거하거나 inactive compatibility path로 남긴다.
* duplicated truth writer가 없는지 확인한다.
* docs와 logs를 구현 결과 기준으로 동기화한다.

완료 조건:

* boss authority send/apply path가 one source / one path로 정리된다.

---

## 8. 리스크

* `MultiplayerPlayerAvatar` old mirror와 new boss bridge가 동시에 apply하면 duplicate boss movement 또는 animator overwrite가 생길 수 있다.
* boss transform truth와 attack animation state truth가 서로 다른 tick 기준을 쓰면 `Basic`/`Lunge` timing이 화면마다 어긋날 수 있다.
* solo-safe boss runtime을 그대로 유지하면서 multiplayer bridge를 얹는 과정에서 `BossController` helper API가 과도하게 커질 수 있다.
* boss HP/HUD를 local `Health`와 authoritative snapshot이 동시에 갱신하면 stale UI 또는 double-refresh가 생길 수 있다.
* `Lunge`는 root motion visual과 authoritative transform이 함께 얽혀 있으므로 visual drift나 landing mismatch가 남을 수 있다.
* current slice에서 aggro를 건드리지 않더라도, target reference handoff와 boss authority bridge 준비 순서가 어긋나면 verify startup에서 target null 상태가 재발할 수 있다.

---

## 9. 검증 계획

### 9.1. 코드 검사

* `dotnet build BossRaidPortfolio.sln`

### 9.2. 수동 검증 시나리오

1. Host와 Client가 gameplay scene 진입 후 같은 boss idle/chase movement를 본다.
2. client local boss는 gameplay AI를 직접 돌리지 않고, Host snapshot만 따라간다.
3. boss `Basic` attack이 Host와 Client 화면에서 같은 시작/진행 리듬으로 보인다.
4. boss `Basic` attack으로 Host player가 맞을 때 HP/HUD/reaction이 Host truth 기준으로 반영된다.
5. boss `Basic` attack으로 Client player가 맞을 때 HP/HUD/reaction이 Host truth 기준으로 반영된다.
6. boss `Lunge` attack이 Host와 Client 화면에서 같은 시작/이동/착지 리듬으로 보인다.
7. player attack으로 boss HP가 줄 때 Host와 Client boss HP UI가 같은 값으로 갱신된다.
8. boss HP가 threshold에 도달하면 phase change가 Host 기준으로 한 번만 시작된다.
9. boss death가 Host와 Client에서 같은 순서로 보인다.
10. old temporary mirror path 제거 후에도 boss가 보이지 않거나 두 번 움직이는 현상이 없다.

### 9.3. 회귀 체크

* solo play boss runtime이 기존처럼 동작해야 한다.
* player action authority path가 boss authority 작업 때문에 direct local attack path로 되돌아가면 안 된다.
* player HUD self/partner authoritative bind가 boss HP sync 추가 때문에 깨지면 안 된다.

---

## 10. 참조 로그

* `docs/Progress_Log/2026-04-01.md`
* `docs/Progress_Log/2026-04-02.md`
* `docs/Progress_Log/2026-04-03.md`

---

## 11. 문서 동기화 계획

구현이 승인되고 완료되면 아래 순서로 문서를 동기화한다.

1. `docs/Progress_Log/YYYY-MM-DD.md`
   작업한 날짜에 맞는 로그 파일을 만들고, 그날 반영한 내용을 그 파일에 작성한다.
2. `docs/Progress_Log/README.md`
3. `docs/technical/System_Blueprint.md`
4. `docs/technical/Technical_Glossary.md`

필요 시 아래 문서도 same-day update 대상으로 본다.

* `docs/technical/multiplayer/boss/Multiplayer_Boss_Authority.md`
* `docs/technical/multiplayer/boss/Multiplayer_Boss_Authority_coding_plan.md`

---

## 12. 승인 게이트

이 문서는 구현 시작 전 승인 baseline이다.

* code implementation은 이 계획서 승인 후에 시작한다.
* 구현 중 scope가 바뀌면 먼저 이 문서를 갱신하고 재승인을 받는다.
* boss aggro나 projectile/AoE full sync가 범위에 들어오면, 이 plan에 same-day scope extension을 먼저 기록한다.
