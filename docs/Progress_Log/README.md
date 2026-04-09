# 🚀 Progress Log: Boss Raid Portfolio

## 🧭 운영 방식
- Progress Log는 docs/Progress_Log/ 폴더 단위로 관리한다.
- 날짜별 기록은 YYYY-MM-DD.md 파일로 분리한다.
- 같은 날짜의 추가 작업은 기존 날짜 파일에 병합하고, 새 날짜 헤더를 만들지 않는다.
- 신규 로그 작성 시 TEMPLATE.md를 복사해 사용한다.

## 🧩 로그 작성 규칙 (신규 엔트리부터 적용)
- 신규 엔트리는 체크리스트 업데이트와 맥락노트를 분리해서 작성한다.
- 기술적 고려에는 아래 3항목을 고정으로 포함한다.
  - **무엇을 발견했는가**
  - **무엇을 수정했는가**
  - **왜 그렇게 판단했는가**
- 코드 변경이 포함된 로그는 코드 검사 결과 블록(명령/결과/미실행 사유)을 반드시 포함한다.
- 장기 작업 목록(마일스톤/버그/폴리싱)은 docs/roadmap/Milestone_Backlog.md에서 관리한다.

## 🔎 문서 동기화 참조 규칙
- `System_Blueprint`/`Technical_Glossary`를 최신화할 때는 기준 로그 파일(`docs/Progress_Log/YYYY-MM-DD.md`)을 먼저 지정한다.
- 완료 보고에는 `참조 로그: docs/Progress_Log/YYYY-MM-DD.md` 형식을 사용해 근거를 남긴다.
- 여러 날짜를 근거로 썼다면 `참조 로그`를 여러 줄로 기록해 추적 가능성을 유지한다.

## 📄 기록 템플릿
- [TEMPLATE.md](./TEMPLATE.md)

## 📅 날짜별 로그
- [2026-04-09.md](./2026-04-09.md) - boss Attack1 bite에 directional front-arc gate 추가, `BasicAttackSettings.hitHalfAngle` 기반 mouth-facing 180-degree 반구 판정 연결, `BossFSM` Basic entry gate 동기화, same-day follow-up으로 attack1/2 warning `show/hide` multiplayer replay를 `BossReplicatedEffectEvent` batch에 확장, remote display-only `AttackWarningController` 재생/cleanup 연결, same-day basic prepare motion follow-up으로 `BossAuthoritativeState`에 attack `normalized time / playback speed` snapshot 추가와 client basic attack host-progress resync 적용, same-day AoE airborne replay fix로 `AttackVisualState` snapshot 추가 및 client `TakeOff -> FlyForward -> FlyIdle -> Land` semantic replay 연결, `BossRaidPortfolio.sln` 빌드 검증, 문서 동기화
- [2026-04-08.md](./2026-04-08.md) - `Tools/Balance/Open Player Boss Balance Tool` editor window 추가, one combined JSON 기반 player/boss balance export/import 경로 구현, prefab target(`MultiplayerPlayerAvatar.prefab`) + verify scene target(`GamePlayScene_Verify.unity`) 동시 지원, selective serialized mapping/문서 동기화, same-day boss `BasicAttackRange` inspector source-of-truth를 `Detection Settings`로 승격하고 `HeadDamageCaster.radius` auto-sync 추가, same-day balance JSON boss `basicAttackRange` export/import + older JSON backward-safe guard 반영, same-day player locomotion `Speed` animator damping helper(default `0.08f`) 추가로 opposite turn idle cut-in 완화, same-day multiplayer owner position correction deadzone debug knob follow-up then rollback, movement trace debug flag + client prediction/correction/render proxy logs 추가, same-day predicted owner locomotion `Speed` single-writer timing fix로 network tick/replay direct animator write를 frame `Update` cache apply로 정리, same-day frame-driven follow-up으로 predicted owner locomotion `Speed`를 current input + current velocity 기반 `Update` 계산으로 추가 정리, same-day planar-speed source swap으로 predicted owner locomotion `Speed` source를 latest predicted planar speed cache로 전환, same-day stop-aware settle rule로 real stop only immediate idle settle(`0.03s` grace) 추가, `BossRaidPortfolio.sln` 빌드 검증
- [2026-04-07.md](./2026-04-07.md) - multiplayer boss HP HUD sync fix, `PlayerController.InitializeCombatHUD()` multiplayer boss source manual mode 전환, `MultiplayerBossAuthorityBridge` boss HUD authoritative snapshot apply/cache reset 추가, multiplayer result Step 1 read surface(`MultiplayerPlayerAvatar.TryGetReplicatedHealth(...)`, `MultiplayerBossAuthorityBridge.TryGetLatestBossState(...)`) 추가, `GameManager` multiplayer victory/both-dead defeat split + `Press Enter to Play (x/2)` retry flow + host-only gameplay restart path 추가, same-day follow-up으로 `GameOver_Panel/Image_Win` / `Image_Lose` panel contract 직접 토글 + `MultiplayerPlayerAvatar.TryGetResultDeathState(...)` dead 판정 helper + dead-player spectator follow(position-only) 추가, same-day camera polish로 spectator follow delay `2.5s` edge timer 추가, retry count latch + short host restart delay로 `1/2 -> 0/2` regression fix, boss aggro priority circle + shared ranged circle 추가, same-day aggro timer refine으로 `AggroTime` cycle/pause/damage-lock 규칙 반영, same-day inspector cleanup으로 detection/aggro settings grouping 정리, same-day HUD portrait follow-up으로 local viewer 기준 portrait swap 추가, same-day aggro retarget follow-up으로 `AggroPriorityRange` 안 current target hold + timer-end winner handoff 규칙 반영, same-day documentation follow-up으로 dedicated aggro source-of-truth `docs/technical/multiplayer/boss/Mutiplayer_Boss_Aggro.md` 추가, same-day aggro internal refactor로 `scan -> resolve -> apply` 구조 정리, same-day boss aggro blog draft `docs/blog/0407_Multiplayer_Boss_Aggro_Rule_and_Flow.md` 추가, `BossRaidPortfolio.sln` 빌드 검증, 문서 동기화
- [2026-04-06.md](./2026-04-06.md) - boss authority `step 1-2` 진행, `BossAuthoritativeState` DTO/enum 추가, `BossController.CaptureAuthoritativeState(...)` boss-side runtime capture surface 추가, active attack id/start time bookkeeping 추가, `MultiplayerBossAuthorityBridge` dedicated runtime-root bridge 추가, client disabled boss display-only apply 연결, `MultiplayerPlayerAvatar` old boss mirror path 제거, same-day trace/session debug cleanup, attempted `step 3` move smoothing rollback, client boss single-consume gate + stable walk-speed follow-up, attack 3/4 projectile/AoE remote display effect replication 추가, `BossRaidPortfolio.sln` 빌드 검증, 문서 동기화
- [2026-04-03.md](./2026-04-03.md) - verify scene solo HP HUD refresh fallback 추가, multiplayer verify용 boss movement + current attack animation state client mirror 추가, client local boss CharacterController disable, Host/Client 양쪽 player HUD HP sync + viewer-side name label(`Host(me)`/`Client(me)`) 반영, boss closest-live-player retarget 추가, player authority `step 8`(`Host approve -> PlayerController execute`) Attack1/hit/stun 실행 경로 고정, client owner attack facing fix와 authoritative `FacingYaw` sync 추가, client dash exit walk-without-input handoff fix, client dash visual smoothness fix, player authority `step 9` current temporary send boundary freeze 문서화, player authority `step 10` current mixed apply boundary freeze 문서화, player authority `step 11` presentation driver visual-only boundary freeze + owner-path gate 정리, multiplayer combo chain Host-authority follow-up, multiplayer attack-cancel dash Host-authority follow-up, 빌드 검증, 문서 동기화
- [2026-04-02.md](./2026-04-02.md) - `MultiplayerConnectionDebug`를 `editor/build + host/client` structured logger로 재정리, `pulse/state-change/gameplay-sync-complete` 추가, avatar profile baseline/transition + `noActionObservedAffected` summary 추가, remote action validation을 `server-buffer` Host receive path로 이동, `StartingGameplay -> InGameplay` state split 및 late disconnect label 정리, guaranteed `peer-disconnect` edge/detail/fallback logger 추가, player HP를 `Host-only write + solo-safe default`로 잠그는 `step 7` health gate 반영, verify scene boss target을 spawned network avatar로 rebind하는 runtime fix 추가, 빌드 검증, 문서 동기화
- [2026-04-01.md](./2026-04-01.md) - `player` action authority step 1-6, Host state/reaction bootstrap, `raw hit log` 기준 경로 추가, action-intent trace/RPC hotfix, predicted dash locomotion smoothing, post-dash animator handoff fix, remote action-intent diagnostic trace, 빌드 검증, authority 문서/블루프린트/용어집 동기화
- [2026-03-31.md](./2026-03-31.md) - `clientPlayer` host locomotion animation sync fix, `AuthoritativeLocomotion` handoff 보강, 빌드 검증
- [2026-03-30.md](./2026-03-30.md) - `PlayerController` 멀티플레이 정리, `LookOnly` 제거, 지연 보정 경계 동기화 1차, 1차 검증 메모, `hardFailShadow` 제거 follow-up, `idleSettle` follow-up, camera-follow jitter trace 추가, owner render proxy follow-up, predicted owner camera orbit tuning, 지터 조사/수정 summary 문서 추가, 블로그 초안 문서 추가, 이동 문서 동기화, 빌드 검증
- [2026-03-27.md](./2026-03-27.md) - predicted render 튜닝 정리, lateral lead 제거, cubic ease-out/`alphaFloor` 보정, 빌드 검증
- [2026-03-26.md](./2026-03-26.md) - Host-only 경로 리셋, prediction/reconcile 연결, 빌드 검증
- [2026-03-25.md](./2026-03-25.md) - 멀티플레이 로컬 소유권 뼈대, legacy `Player` 제거, `hostPlayer`/`clientPlayer` 이름 고정
- [2026-03-24.md](./2026-03-24.md) - 멀티플레이 런타임·패키지 정리, shared baseline 복구, 브랜치 문서 동기화
- [2026-03-18.md](./2026-03-18.md) - 멀티플레이 테스트 씬 통합, partner HUD/콤보 HUD 게이트, 메인 gameplay 씬 승격
- [2026-03-17.md](./2026-03-17.md) - Client join 런타임, Lobby Events 컴파일 안정화
- [2026-03-16.md](./2026-03-16.md)
- [2026-03-13.md](./2026-03-13.md)
- [2026-03-12.md](./2026-03-12.md)
- [2026-03-11.md](./2026-03-11.md)
- [2026-03-06.md](./2026-03-06.md)
- [2026-03-05.md](./2026-03-05.md)
- [2026-03-04.md](./2026-03-04.md)
- [2026-03-03.md](./2026-03-03.md)
- [2026-02-28.md](./2026-02-28.md)
- [2026-02-27.md](./2026-02-27.md)
- [2026-02-26.md](./2026-02-26.md)
- [2026-02-24.md](./2026-02-24.md)
- [2026-02-23.md](./2026-02-23.md)
- [2026-02-21.md](./2026-02-21.md)
- [2026-02-20.md](./2026-02-20.md)
- [2026-02-12.md](./2026-02-12.md)
- [2026-02-11.md](./2026-02-11.md)
- [2026-02-10.md](./2026-02-10.md)
- [2026-02-09.md](./2026-02-09.md)
- [2026-02-06.md](./2026-02-06.md)
- [2026-02-05.md](./2026-02-05.md)
- [2026-02-04.md](./2026-02-04.md)
- [2026-02-03.md](./2026-02-03.md)
- [2026-02-02.md](./2026-02-02.md)

## 🗂️ Milestone Backlog
- [Milestone_Backlog.md](../roadmap/Milestone_Backlog.md)

## 📦 Legacy
- [LEGACY_MONOLITH.md](./LEGACY_MONOLITH.md) (분할 전 단일 파일 원본 보관)
