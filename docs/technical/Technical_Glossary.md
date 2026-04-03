# 📖 Technical Glossary: Boss Raid Portfolio

이 문서는 프로젝트 내에서 통용되는 주요 용어와 개념을 정의합니다.

## 1. Character Entities

* **The Capsule (플레이어)**: 플레이어가 조작하는 캐릭터 객체. 현재 그래픽이 캡슐 형태이므로 내부적으로 'Player' 또는 'TheCapsule'로 지칭한다.
* **The Cube (보스)**: 레이드 대상인 AI 보스 객체. 정육면체 형태이며, 'Boss' 또는 'TheCube'로 지칭한다.
* **CameraRoot**: 플레이어 이동 기준 축으로 사용하는 카메라 앵커 Transform. 런타임 시작 시 플레이어 자식에서 분리되어 월드 공간에서 위치/방향을 추적한다.
* **ThirdPersonCameraController**: 메인 카메라에 부착되는 3인칭 카메라 전용 컨트롤러(`Assets/Scripts/Camera/ThirdPersonCameraController.cs`). 카메라 추적/회전 설정을 카메라 오브젝트에서 직접 관리한다.
* **BossVisual**: 보스(`The Cube`)의 애니메이션, UI, 이펙트 등 시각적 요소를 전담하는 컴포넌트. `BossController`의 로직과 분리되어 있다.

## 2. Input & Branch Policy

* **Input Packet (PlayerInputPacket)**: 매 프레임 발생하는 입력 데이터를 담은 구조체. `moveDir`, `lookYaw`, `lookPitch`, `buttons`를 포함한다.
* **Input Provider**: 입력을 생성하는 주체. `LocalInputProvider`(키보드/마우스)와 추후 구현될 `NetworkInputProvider`(RPC/데이터 패킷)로 나뉜다.
* **MultiplayerBufferedInputProvider**: Host가 remote owner 입력 패킷과 latest input sequence를 유지하기 위해 사용하는 `IInputProvider` 구현. client input uplink가 host-authority simulation으로 들어가는 임시 버퍼 역할을 맡는다.
* **LookOnly Simulation Mode (제거됨)**: 과거 Host-only local presentation 경로에서 쓰던 `PlayerController.RuntimeSimulationMode`. 2026-03-30 cleanup으로 runtime enum, driver branch, prefab serialization에서 제거했고, 현재 코드는 이 모드를 사용하지 않는다.
* **Half Prediction (반쪽 prediction)**: client local prediction은 켜지만 rollback/replay 없이 Host correction만 받는 중간 구조. first feel은 빨라질 수 있지만 owner-local double writer와 correction tug-of-war 때문에 jitter를 다시 만들기 쉽다.
* **Rollback/Replay Locomotion**: owner client가 입력 tick별 predicted state를 저장하고, Host authoritative state가 오면 그 tick sim state로 되감은 뒤 아직 ack되지 않은 입력을 다시 재생하는 방식. 2026-03-26 `Path B Phase 5` 기준으로 `PredictionReconciliation` switch path의 locomotion-only scope에서 now active다.
* **PredictedLocomotion Simulation Mode**: owner client 전용 locomotion prediction 모드. local move/rotate는 즉시 실행하지만 final truth는 Host snapshot이 가진다. 2026-04-01 follow-up 기준 `walk + dash` 는 이 same predicted locomotion path 안에 남고, `Attack / Jump` 같은 non-locomotion input만 full authoritative fallback으로 내려간다.
* **AuthoritativeLocomotion Simulation Mode**: Host 전용 fixed-tick locomotion authority 모드. owner client와 같은 move/rotate/gravity helper를 사용하지만 final state를 결정하는 쪽은 Host다. 2026-03-26 `Path B Phase 4` 기준으로 `PredictionReconciliation` path에서 실제 사용된다. 2026-03-31 fix에서는 Host authority replica가 move-only mode로 재진입할 때 remote avatar animator를 `Locomotion` 으로 다시 맞추고, fixed-tick locomotion sim에서 `Speed` 도 함께 갱신해 host 화면의 client idle/walk 누락을 막는다.
* **Authoritative Locomotion Snapshot (`MultiplayerLocomotionState`)**: `inputSequence`, `serverTick`, `position`, `yaw`, `planarVelocity`, `verticalVelocity`, `jumpTimer`에 더해 `dash timer`, `dash cooldown timer`, `last buttons`, grounded/prediction/dash-active flag를 담는 직렬화 struct. current reset path에서는 owner replay 기준이라기보다, Host가 current authoritative transform/velocity + dash runtime 상태를 owner에게 내려주는 snapshot으로 쓴다.
* **MultiplayerLocomotionInput**: Path B `Phase 1`에서 explicit하게 분리한 locomotion input network contract. `inputSequence`, `moveDirection`, `lookYaw`, `lookPitch`, `buttons`를 한 struct로 묶어 owner uplink, host buffer, replay history가 같은 입력 단위를 공유하게 한다. 2026-03-26 `Phase 3~5` 기준으로 prediction/authority/replay 모두 이 입력 단위를 기준으로 동작한다.
* **Shared CharacterController Locomotion Core (`PlayerLocomotionCore`)**: Path B `Phase 2` foundation에서 시작한 shared locomotion capture / apply / simulate entry point. current refactor slice에서는 `Assets/Scripts/Player/PlayerLocomotionCore.cs`로 분리되어, Host sim과 owner prediction이 같은 `CharacterController`-based move / rotate / gravity / grounded rule을 계속 공유한다. 2026-04-01 follow-up 기준 shared core는 walk뿐 아니라 dash start/duration/cooldown도 함께 계산하고, dash exit animator handoff에서도 raw input 한 틱 값 대신 actual planar speed를 함께 반영한다. `PlayerController`는 compatibility wrapper만 유지한다.
* **MultiplayerPlayerPresentationDriver**: `Assets/Scripts/Multiplayer/Gameplay/MultiplayerPlayerPresentationDriver.cs`에 있는 multiplayer local presentation 전용 driver. current code에서는 `PredictedLocomotion` owner path의 local presentation과 owner render proxy를 소유한다. 2026-03-30 follow-up 기준 free move owner 화면은 raw root를 그대로 보지 않고, visual child와 camera가 같은 `render proxy` surface를 보도록 연결한다. 2026-04-01 cleanup으로 old predicted render trace hook은 제거됐다.
* **Reliable Input Sequence Gate**: owner -> Host input RPC를 reliable ordered delivery로 유지하는 규칙. dropped input 하나 때문에 server consume/replay queue가 멈추는 위험을 줄이기 위한 안전장치다.
* **Shadow Authoritative State (그림자 authoritative state)**: owner client가 free move 중 수신한 Host locomotion snapshot을 즉시 root에 적용하지 않고 임시 진실값으로만 저장해 두는 상태. current lazy boundary correction path에서는 drift 측정과 stale 판단 기준으로 사용한다. 2026-03-30 smoke test에서는 `현재 predicted state` 를 `latest shadow state` 와 바로 비교하면 drift가 과장되는 문제가 확인됐고, current follow-up code에서는 그 tick-side compare를 제거했다.
* **Lazy Boundary Correction (지연 보정 경계 동기화)**: owner free move에서는 Host snapshot을 곧바로 reconcile하지 않고, `boundary sync / hard fail / stale sync / idleSettle` 에서만 즉시 다시 맞추는 규칙. current code의 핵심 correction policy다. 2026-03-30 follow-up 기준으로 old shadow snapshot compare 기반 `hardFailShadow` 는 current active path에서 제거됐다.
* **Boundary Sync (경계 동기화)**: 이전 tick에서는 free-move prediction이 가능했지만 이번 tick에서는 불가능해지는 전환을 경계로 보고, 다음 authoritative snapshot에서 즉시 동기화하는 규칙. current follow-up 기준 `Dash` 는 predicted locomotion path에 남고, `Attack / Jump` 입력 시작이나 `MoveState` 이탈 쪽이 이 경계를 더 직접적으로 만든다.
* **Hard-Fail Sync (강제 실패 동기화)**: owner predicted state와 Host authoritative state 오차가 큰 경우, lazy wait를 중단하고 즉시 authoritative baseline으로 돌아가는 규칙. current 1차 기준은 `0.65m / 25deg` 다. authoritative packet arrival 시 same-sequence compare 쪽이 더 안전한 기준으로 본다.
* **HardFailShadow (그림자 강제 실패)**: owner free move tick에서 current predicted state를 latest shadow authoritative state와 직접 비교해 즉시 sync하던 1차 임시 규칙. 2026-03-30 smoke test 기준 normal free move에서도 자주 발동해 lazy correction을 깨는 known issue가 확인됐고, current follow-up code에서는 제거됐다. old console log를 읽을 때만 historical phase 이름으로 남는다.
* **Stale Sync (오래된 snapshot 동기화)**: owner가 마지막 shadow authoritative state를 너무 오래 새로 받지 못한 경우, 다음 authoritative snapshot에서 즉시 동기화로 들어가게 하는 안전 규칙. current 1차 기준은 `300ms` 다.
* **Idle Settle (정지 정렬 동기화)**: free move 중 hard-fail 아래의 medium drift가 남아 있어도, owner predicted state와 authoritative state의 planar speed가 둘 다 거의 0이면 즉시 settle시키는 규칙. current follow-up 기준 planar speed threshold는 `0.05` 다.
* **Free-Move Ignore Zone (자유 이동 무시 구간)**: owner free move에서 correction을 일부러 하지 않고 drift만 기록하는 허용 범위. current 1차 기준은 `0.20m / 10deg` 다.
* **Correction Deadzone**: 이전 correction path에서 owner predicted state와 Host authoritative state 차이가 아주 작을 때 rewind/replay correction을 건너뛰던 규칙. current lazy boundary correction path에서는 main sync rule로 쓰지 않고 historical small-error term으로 남는다.
* **Duplicate Authoritative Sequence Ignore**: owner가 이미 처리한 같은 authoritative input sequence를 다시 받았을 때, locomotion prediction mode에서는 중복 correction을 건너뛰는 규칙. 늦은 새 입력을 기다리는 동안 repeated micro rewind가 생기는 것을 줄인다.
* **Deterministic Yaw Replay**: locomotion replay 시 현재 Transform rotation에 바로 의존하지 않고, saved state yaw에서 target yaw로 같은 수식으로 갱신하는 규칙. tiny rotate mismatch를 줄이기 위한 follow-up이다.
* **Deterministic Kinematic Locomotion Motor**: multiplayer locomotion tick에서 `CharacterController.Move()`를 replay core로 쓰지 않고, capsule sweep + wall slide + ground snap 순서로 next locomotion state를 계산하려던 전용 motor 실험. 2026-03-26 Path B follow-up 이후 active build path에서는 제거했고, current chosen direction은 shared `CharacterController` locomotion core 쪽이다.
* **Shared Pure Locomotion Simulator**: latest reference-based pass에서 도입했던 multiplayer locomotion sim core experiment. owner prediction과 Host authority가 같은 sim state를 입력으로 받아 next state를 계산하려던 목적이었지만, current chosen direction은 이것을 active path로 유지하는 대신 shared `CharacterController` locomotion core로 다시 정리하는 쪽이다.
* **Host-Only CharacterController Path (레거시 제거 경로)**: 과거에는 `client sends input -> Host simulates -> client applies Host snapshot` 흐름으로 유지했던 Host-only path. 2026-03-30 cleanup으로 current runtime code와 prefab selection에서 제거했고, 지금은 historical reference로만 남는다.
* **Separate Action-Intent Path**: locomotion input packet과 분리된 multiplayer action uplink 경로. first implementation step에서는 `dash / Attack1` 입력 edge만 별도 packet으로 Host에 보내고, movement packet과 result packet을 섞지 않기 위한 계약 역할을 맡는다.
* **HostPlayerState**: Host가 player 1명에 대해 유지하는 authoritative runtime state struct. `HP`, active action, accepted action sequence/start tick, consumed reaction sequence, hit/stun/death flag를 담고 이후 replication의 source state가 된다.
* **HostToClientPlayerReactionSnapshot**: Host가 one-shot reaction result를 전달하기 위해 만드는 snapshot struct. `reaction sequence`, `server tick`, `damage`, `result HP`, `source hit type`, `interrupted action`을 함께 담아 later client dedupe/apply의 기준이 된다.
* **HostPlayerReactionResolver**: Host-side authority helper. current runtime state를 `HostPlayerState`로 동기화하고, boss hit result에서 `HostToClientPlayerReactionSnapshot`을 만들며, player attack damage result를 `server tick` 기준 `raw hit log`로 기록한다.
* **Runtime Health Write Gate**: `Health`가 가진 runtime HP write authority rule. default는 writable이라 solo play와 기존 boss damage flow를 그대로 유지하고, multiplayer에서는 `MultiplayerPlayerAvatar`가 runtime role에 따라 Host-owned player / Host authority replica만 writable, client owner / client replica는 read-only로 바꿔 `TakeDamage`/`Heal` 최종 쓰기를 Host 쪽으로 잠근다.
* **Boss Client Presentation Mirror**: current verify multiplayer unblock path. dedicated boss network actor 대신 `MultiplayerPlayerAvatar`가 Host authority replica에서 boss `position / rotation / animator state / normalized time / Speed` snapshot을 owner client로 보내고, client는 local disabled boss object에 이를 display-only로 apply한다. movement와 current attack animation state는 보이지만 projectile/AoE object replication은 아직 포함하지 않는다.
* **Raw Hit Log**: later boss aggro / contribution rule을 위해 남기는 Host-only damage record. current step `4~6` 기준으로 `dealer client id / action sequence / action flag / damage / server tick`을 한 묶음으로 저장한다.
* **PredictionReconciliation**: current multiplayer movement runtime path의 큰 이름. owner client는 locomotion input을 즉시 predict하고, Host authority replica는 shared `PlayerLocomotionCore`로 같은 입력을 authoritative하게 sim한 뒤 snapshot을 내려준다. 2026-04-01 follow-up 기준 dash도 이 shared locomotion sim 안에서 계산하며, owner는 `walk + dash` 동안 prediction을 유지한다. `Attack / Jump` 같은 non-locomotion input은 fallback/boundary 쪽 authoritative path로 내린다. same-day animator follow-up에서는 owner replay/apply도 dash exit 직후 `actual planar speed` 를 함께 사용해 idle flash를 덮어쓰지 않도록 맞췄다. 2026-03-30 lazy correction 1차 이후 owner는 free move snapshot을 우선 `shadow authoritative state` 로 저장하고, baseline/fallback/boundary/hard-fail/stale 시점에서만 immediate sync + replay를 수행한다.
* **Runtime Role Configuration Log**: `MultiplayerPlayerAvatar` spawn 시 남기는 diagnostic log. current code에서는 fixed `PredictionReconciliation` path를 전제로 `predictionPath=PredictionReconciliation / server / owner / mode`를 한 줄로 출력한다.
* **MultiplayerActionIntentTrace**: `MultiplayerPlayerAvatar`가 dash/attack action edge를 관측하거나 Host validator / Host state / reaction snapshot / raw hit log 결과를 남길 때 쓰는 diagnostic log. current `step 1~6`에서는 `observe`, `validate`, `host-state`, `reaction-snapshot`, `raw-hit-log` phase를 통해 action authority 흐름을 읽는다. current follow-up 기준 remote player의 Host truth는 `server-buffer` source에서 `buffer-observe -> validate -> host-state` 로 이어지고, `rpc-received` 는 separate action-intent ServerRpc receive trace-only phase로 남는다.
* **MultiplayerConnectionDebug**: `MultiplayerSessionService`가 남기는 session continuity structured logger. current event set은 `peer-connect`, `peer-disconnect`, `state-change`, `gameplay-sync-complete`, `pulse`이며, line은 `editor/build + host/client`, `state`, `scene`, `localClientId`, `isServer`, `isClient`, `isConnected`, `isListening`, `shutdown`, `hostPeerCount`, `lobbyPlayers`를 함께 싣는다. 2026-04-02 follow-up 기준 Host state flow는 `LobbyActive -> StartingGameplay -> InGameplay` 이고, `StartingGameplay` 는 scene handoff 동안만, `InGameplay` 는 sync 완료 후 runtime gameplay 동안만 사용한다. disconnect logger는 `peer-disconnect` minimal edge line, `peer-disconnect-detail` profile line, `peer-disconnect-fallback` shutdown backup line으로 나뉘어 exact disconnect edge를 더 안전하게 남긴다. avatar profile dump와 `noActionObservedAffected` summary는 detail line에 붙여 어느 앱에서 어떤 입력 조건으로 끊겼는지 빠르게 읽게 한다.
* **InGameplay Session State**: gameplay scene sync와 authoritative player spawn이 끝난 뒤의 runtime multiplayer state. `StartingGameplay` 와 분리되어 late disconnect를 `Client disconnected during gameplay.` 로 기록하게 만들고, lobby session state도 `InGame` 값으로 publish하는 기준이 된다.
* **Disconnect Input Profile**: `MultiplayerPlayerAvatar`가 owner input / Host buffered input을 따라가며 누적하는 disconnect 전용 입력 요약. current label은 `idle-only`, `idle-walk-only`, `action-observed` 세 가지이며, `avatar-profile-baseline` / `avatar-profile-transition` event로 disconnect 이전부터 어떤 단계까지 갔는지 기록한다.
* **Client Prediction Movement Trace (historical)**: real `PredictionReconciliation` owner path를 읽기 위해 쓰던 old runtime log. `[MultiplayerClientMoveTrace]` prefix로 `predict`, `fallback`, `boundarySync`, `hardFail`, `staleSync`, `idleSettle`, `shadow`, `defer` 등을 기록했지만, 2026-04-01 cleanup에서 current code path에서는 제거됐다. old console log나 investigation 문서를 읽을 때만 historical term으로 참고한다.
* **Predicted Render Trace Hook (historical)**: owner free move visual이 raw root target과 얼마나 벌어지는지 확인하기 위해 쓰던 old local diagnostic log. `[MultiplayerPredictedRenderTrace]` prefix를 사용했지만, 2026-04-01 cleanup에서 제거됐다. current code에서는 `render proxy` presentation만 유지하고 trace hook은 남기지 않는다.
* **Multiplayer Camera Follow Trace (historical)**: current owner 화면 jitter를 직접 읽기 위해 한때 `ThirdPersonCameraController` 에 추가했던 old runtime log. `[MultiplayerCameraFollowTrace]` prefix를 사용했지만, 2026-04-01 cleanup에서 제거됐다. old measurement read는 progress log와 jitter investigation 문서에서만 참고한다.
* **Predicted Owner Tight Follow**: predicted owner camera path에서 render proxy orbit을 다시 부드럽게 늦게 쫓지 않도록, active position/rotation smoothing을 더 직접적으로 두는 follow-up 규칙. current 기본값은 `posSmooth=0`, `rotSmooth=0` direct orbit follow이며, old camera trace 자체는 2026-04-01 cleanup에서 제거됐다.
* **Predicted Render Tick Interpolation**: 과거 `PredictedLocomotion` local owner visual child가 predicted tick state 사이를 render frame에서 보간하던 presentation rule. 2026-03-30 lazy correction 1차 이후 current free-move owner path에서는 active presentation rule로 사용하지 않고, 이전 튜닝 이력 용어로 남는다.
* **Predicted Transition Snap**: `PredictedLocomotion` local owner visual child가 sharp move-angle change를 만났을 때만 한 번 current predicted target으로 바로 붙는 presentation rule. steady move는 tick interpolation을 유지하고, transition frame의 남은 slight strafe jitter만 줄이는 것이 목적이다.
* **Predicted Render Lateral Lead**: pure `A / D` strafe one-tick offset을 줄이기 위해 시도했던 experiment rule. local predicted visual body가 current predicted target에서 predicted tick delta 방향으로 조금 앞서 그려지게 했지만, grouped trace에서는 `0.0`이 가장 낮은 `behindTicks`를 보였고 higher lead values가 더 나빴다. current active path에서는 이 branch를 사용하지 않는다.
* **Avatar Inspector Guide**: `MultiplayerPlayerAvatar` custom inspector 상단 help box. `NetworkObject`, `NetworkTransform`, `MultiplayerBufferedInputProvider`, `MultiplayerPlayerAvatar`, `NetworkAnimator`가 각각 무엇을 하는지 easy English로 짧게 설명해 component stack 이해를 돕는다.
* **Initial Authoritative Baseline Sync**: Path B startup follow-up rule. owner client는 spawn 직후 local current transform을 오래 prediction 기준점으로 쓰지 않고, first Host authoritative locomotion state를 startup baseline으로 먼저 수용한 뒤 normal prediction/replay를 시작한다. very small startup reconcile jitter를 줄이기 위한 narrow fix다.
* **Explicit Multiplayer Tick Rate**: `MultiplayerRuntimeRoot`가 multiplayer runtime에서 NGO `NetworkConfig.TickRate`를 명시적으로 설정하는 규칙. 2026-03-26 current verify path에서는 `60`을 사용하고, startup log로 active tick rate를 한 줄 출력해 Path B walking jitter가 tick-step cadence 문제인지 확인하는 기준으로 쓴다.
* **Predicted Render Smoothing**: `PredictedLocomotion` owner path에서 render frame 기준 proxy를 부드럽게 따라오게 하는 presentation rule. current follow-up에서는 visible body child만 따로 smoothing하는 것이 아니라, `Owner Render Proxy` 를 만들고 camera와 body가 둘 다 같은 proxy를 보도록 연결한다.
* **Single-Root Presentation (단일 루트 화면 기준)**: 2026-03-30 lazy correction 1차에서 잠시 사용했던 follow-up presentation rule. free move owner 화면에서 extra world-space smoothing을 끄고 raw root/body/camera timing을 최대한 맞추려던 시도였지만, 후속 `[MultiplayerCameraFollowTrace]` 측정 결과 raw root tick-step jitter가 더 직접적인 원인으로 확인되면서 current active path에서는 `shared render proxy` 쪽으로 다시 넘어갔다.
* **Boss Room-Style Latency Masking (과거 참고안)**: gameplay root/collider truth는 Host authoritative snapshot에 맡기고, local owner visual child와 camera follow anchor에만 보정을 적용하던 과거 reference path. 2026-03-30 cleanup 이후 current code path에서는 active logic로 유지하지 않고, historical study reference로만 남긴다.
* **Multiplayer Presentation Trace Hook (제거됨)**: local owner `LookOnly` presentation layer를 읽기 위한 임시 진단 로그. `LookOnly` 제거와 함께 current code path에서 제거됐다.
* **Medium Moving Catch-Up (과거 보정 규칙)**: old Host-only masking path에서 moving 초반 `visualTargetOffset`을 줄이기 위해 쓰던 body-position catch-up rule. current predicted render path에서는 사용하지 않는다.
* **Tick-Clean Input Capture**: `LocalInputProvider`가 move/buttons를 frame-cached packet으로 유지하고 `GetInput()`은 그 값을 반환하는 규칙. camera update와 network tick sampling이 완전히 같은 순간은 아니어도, loose direct polling보다 더 안정된 input snapshot을 제공한다.
* **Owner Double-Writer Jitter**: local owner movement와 Host authoritative correction이 같은 avatar root를 서로 다른 timeline으로 만질 때 생기는 흔들림. owner client는 remote interpolation 대상이 아니므로, smoothing보다 rollback/replay로 해결해야 한다.
* **Delay Rejection Rule (delay 비채택 규칙)**: `1~2 sec` 같은 큰 delay는 multiplayer action control 문제의 해결책으로 채택하지 않는 규칙. input start delay나 big delayed correction은 jitter를 숨기지 못하고 responsiveness만 크게 떨어뜨린다.
* **Bit-Packing (비트 패킹)**: 여러 개의 `bool` 버튼 상태를 1바이트(`byte`) 데이터로 묶어 네트워크 전송 효율을 극대화하는 기법.
* **Input Flag**: 비트 패킹 시 각 버튼의 자릿수를 지정하는 `Enum` (예: Dash, Attack).
* **Main-Safe Shared Branch (main-safe shared branch)**: `main`이 UI/art/solo-safe shared branch로 유지되는 운영 규칙. 이 브랜치는 `Assets/Scripts/Multiplayer/**`, duplicated multiplayer scenes, UGS/NGO package set 없이도 compile/run 가능해야 한다.
* **Multiplayer Branch Ownership (멀티플레이 브랜치 소유권)**: real multiplayer runtime, duplicated scenes, partner HUD activation, Relay/Lobby/NGO package set을 `feature/multiplayer`가 단일 owner로 갖는 규칙.
* **Title Multiplayer Prototype (타이틀 멀티플레이 프로토타입)**: `TitleSceneController`가 `main`에서도 `Host/Client/Lobby/WrongKeyPopup` panel flow를 local prototype UI로 유지하는 상태. layout/UX/state transition 확인용이며, real service bootstrap은 포함하지 않는다.
* **Shared Art Package Add-on (shared art package add-on)**: shared `main`이 유지할 수 있는 art-side package 확장. 현재 허용 범위는 `com.unity.2d.sprite`, `com.unity.formats.fbx`이며, network/service package set은 여기에 포함하지 않는다.

## 3. Architecture & Logic

* **FSM (Finite State Machine)**: 캐릭터의 행동 상태(Idle, Move, Attack 등)를 관리하는 유한 상태 머신.
* **StateMachine**: `BaseState`를 교체하며 라이프사이클(`Enter`, `Update`, `Exit`)을 관리하는 핵심 제어기.
* **BaseState**: 모든 상태 클래스가 상속받는 추상 클래스. `PlayerController` 참조를 가지며 실제 로직을 수행한다.
* **State Delegate**: `PlayerController`가 자신의 로직 처리를 현재 활성화된 `BaseState` 객체에 넘겨주는 행위.
* **Namespace (네임스페이스)**: 코드를 논리적으로 그룹화하고 클래스 이름 충돌을 방지하는 주소 체계. (예: `BossRaid.Patterns`)
* **Character Motor**: 실제 `CharacterController.Move()`를 실행하여 물리적인 이동을 처리하는 로직부.
* **Edge-Triggering (엣지 트리거)**: 입력 신호가 변하는 순간(예: 버튼을 누르는 찰나)을 포착하여 한 번만 로직을 실행하는 기법.
* **Cooldown (쿨타임)**: 기술 재사용 대기시간. `Time.time`을 기준으로 다음 실행 가능 시간을 계산하여 관리함.
* **Coupling (결합도)**: 두 모듈 간의 의존 정도. `Strong Coupling`은 변경에 취약하고, `Weak Coupling`은 인터페이스 등을 통해 유연하다.
* **Dependency Injection (의존성 주입)**: 객체가 의존하는 다른 객체를 직접 생성(`new`)하지 않고 외부에서 주입받아 결합도를 낮추는 패턴.
* **Visual Separation (비주얼 분리)**: 핵심 로직(`Controller`)과 시각적 표현(`Visual`)을 서로 다른 클래스로 분리하여, 로직 변경이 리소스(애니메이션 등)에 영향을 주지 않도록 하는 설계 패턴.
* **Camera Anchor Decoupling (카메라 앵커 분리)**: 카메라 앵커를 플레이어 자식 계층에서 분리해 부모 회전 상속을 제거하는 구조. 좌우 회전 시 카메라 지터를 줄이고 이동 기준 축을 안정화한다.
* **Mouse Primary Camera Control (마우스 1차 제어)**: 카메라 yaw/pitch는 `lookYaw/lookPitch` 입력을 기준으로 동작하는 기본 제어 규칙.
* **Hidden Camera Smoothing Data (숨김 카메라 스무딩 데이터)**: `positionSmoothTime`/`rotationSmoothTime`는 직렬화 데이터로 유지하지만, 현재 인스펙터에는 노출하지 않는 운영 방식. 현재 기본값은 둘 다 `0.01f`다.
* **Hidden Auto-Behind Assist Data (숨김 자동 후방 정렬 데이터)**: `autoBehindAssist` 및 관련 파라미터는 런타임/직렬화 데이터로 유지하지만, 현재 인스펙터에는 노출하지 않는 운영 방식.
* **Editor Auto-Attach (에디터 자동 부착)**: 플레이 모드뿐 아니라 에디터 로드 시점에도 `Main Camera`에 카메라 컨트롤러를 자동으로 추가해, 플레이 전 인스펙터 튜닝이 가능하도록 만드는 처리.
* **Inspector Tooltip Guidance (인스펙터 툴팁 가이드)**: 파라미터 의미를 쉽게 이해할 수 있도록 카메라 필드에 짧은 쉬운 영어 설명을 부여하는 규칙.
* **CombatHUDController**: 전투 HUD 전용 컨트롤러. 플레이어/보스 HP 바, 이름 라벨, 고정형 데미지 텍스트를 한 컴포넌트에서 제어한다. 현재는 shared canvas 안의 `PartnerHUD_Panel` HP/name slot과 `Text_Combo` combo visibility gate도 함께 소유한다.
* **HUD 이름 라벨 정책**: `Text_PlayerHP`/`Text_BossHP` 슬롯을 체력 수치 대신 이름 라벨(`Player`, `Dragon`) 표시 용도로 사용하는 UI 정책.
* **HUD 부트스트랩 바인딩**: `PlayerController.InitializeCombatHUD()`에서 HUD 참조/보스 `Health`를 초기 탐색한 뒤 `Initialize`, 이름 라벨 세팅, partner HUD visibility gate 적용, combo default hide까지 한 번에 수행하는 시작 절차. solo에서는 local player `Health`를 HUD source로 넘기고, multiplayer에서는 player source를 `null`로 둔 manual mode로 전환한다.
* **Visual Root Merge (시각 루트 병합)**: gameplay scene의 로직 루트는 유지한 채, 다른 scene에서 준비한 `Canvas` / `Background` / `Tile` 같은 시각 전용 root만 옮겨 붙이는 병합 방식. 이번 gameplay scene 정리에서는 검증 후 main gameplay scene으로 승격했고, multiplayer gameplay scene duplicate의 source로도 재사용됐다.
* **HP Fill 정규화 업데이트**: `HealthRatio`를 `Image.fillAmount`로 반영해 체력 UI를 갱신하는 방식. 수치 텍스트 갱신 없이도 이벤트 기반으로 즉시 동기화할 수 있다.
* **HUD Health Refresh Fallback**: `CombatHUDController`가 `LateUpdate()`에서 player/boss `CurrentHealth`/`MaxHealth` 변화를 다시 비교해, event가 이미 지나갔거나 bind timing이 어긋난 경우에도 HP fill이 stale full state로 남지 않게 하는 safety net.
* **HUD 가시성 토글**: `CombatHUDController.ShowHud(bool)`로 플레이어/보스 체력 UI와 이름 라벨, 데미지 피드백 표시를 일괄 On/Off 하는 제어 패턴.
* **Partner HUD Visibility Gate (파트너 HUD 표시 게이트)**: `CombatHUDController`가 `PartnerHUD_Panel`을 기본 hidden으로 유지하는 규칙. solo에서는 `PlayerController.InitializeCombatHUD()`가 `SetPartnerHudVisible(false)`를 유지하고, active multiplayer session에서는 local-owned `MultiplayerPlayerAvatar`가 `SetPartnerHudVisible(true)`와 actual HP/name binding을 함께 연다.
* **Multiplayer HUD Health Replica (멀티플레이 HUD 체력 복제본)**: `MultiplayerPlayerAvatar`가 server-authoritative player `current/max HP`를 HUD 전용 `NetworkVariable<int>` pair로 유지하는 경량 스냅샷. gameplay truth를 다시 계산하는 용도가 아니라, Host/Client 양쪽 화면의 player HUD가 같은 HP source를 읽게 만드는 display sync 경로다.
* **Viewer-Side HUD Naming (화면 기준 HUD 이름 규칙)**: 같은 network player라도 보고 있는 화면의 local ownership에 따라 라벨을 다르게 쓰는 규칙. Host 화면은 `Host(me)` / `Client`, Client 화면은 `Client(me)` / `Host`를 사용한다.
* **Combo UI Visibility Gate (콤보 UI 표시 게이트)**: `CombatHUDController`가 `Text_Combo` root를 기본 hidden으로 유지하고, `AttackState.StartComboStep()`은 `PlayerController.SetPendingComboHudStep(step)`로 현재 step만 준비한다. 실제 combo UI open은 `DamageCaster.OnAttackHitConfirmed` -> `PlayerController.ShowComboHud(step)` -> `CombatHUDController.ShowCombo(step)` 경로에서만 수행되며, miss 공격은 UI를 열지 않는다. `AttackState.Exit()`, `PlayerController.HandleDeath()`, `CombatHUDController.ShowHud(false)`는 `HideCombo()`로 stale combo UI를 정리한다.
* **Title Scene**: 게임 시작 시점 전용 씬. 현재 `TitleSceneController`가 버튼 기반 `Solo Play / Multi Play` 흐름과 local Host/Client/Lobby prototype panel 전환을 관리하며, `TitleRuntimeRoot`를 Edit Mode에서도 재사용할 수 있도록 유지한다.
* **Multiplayer Branch Isolation (멀티플레이 브랜치 격리)**: real multiplayer 코드를 shared `main`에 남기지 않고 `feature/multiplayer` 브랜치에서만 유지하는 운영 방식. shared `main`은 UI/art/solo-safe baseline을 우선한다.
* **Scene GUID-Preserving Promotion (씬 GUID 보존 승격)**: Unity scene를 최신 duplicate로 교체할 때 target `.meta` GUID는 유지하고 `.unity` 본문만 교체하는 승격 방식. build settings와 scene asset reference를 흔들지 않으면서 latest scene content를 main path로 올릴 때 사용한다. 2026-03-18에는 latest temporary gameplay scene content를 `Assets/Scenes/GamePlayScene.unity`로 올릴 때 이 방식을 사용했다.
* **Loading Scene**: 씬 전환 중 비동기 로드를 담당하는 중간 씬. `SceneLoader`가 목적지 씬을 예약하고 `LoadingSceneController`가 진행률 UI와 활성화 타이밍을 제어한다.
* **Input Lock Duration (입력 잠금 시간)**: 타이틀 진입 직후의 잔존 키 입력으로 즉시 전환되는 문제를 막기 위한 짧은 대기 구간. 현재 `TitleSceneController`의 `_inputLockDuration`으로 제어한다.
* **TitleMainPanel**: `TitleScene`의 첫 진입 패널. `Solo Play`, `Multi Play` 두 버튼만 노출한다.
* **MultiplayerModePanel**: 멀티플레이 진입 후 `Host`, `Client`, `Back to Title` 분기를 고르는 패널.
* **HostCreatePanel**: Host가 optional room title을 입력하고 `Create Room`을 누르는 패널. 제목이 비면 `join here 0000` 형식 auto title을 생성한다.
* **ClientJoinPanel**: Client가 6자리 join code를 입력하는 패널. shared `main`에서는 format validation + wrong key popup UX를 확인하는 local prototype step으로 사용한다.
* **LobbyPanel**: Host/Client가 공통으로 보는 멀티플레이 대기 패널. shared `main`에서는 room title, join code, connected players, waiting text, host-only `Start`를 local prototype state로만 표시한다.
* **WrongKeyPopup**: 잘못된 join code를 같은 UX로 묶어 보여주는 overlay popup. 기본 문구는 `Wrong key. Please type again.` 이다.
* **MultiplayerPlayerAvatar**: gameplay scene에서 각 network player object의 runtime role을 정하는 `NetworkBehaviour`. Host-owned authority replica, client-owned local presentation, remote proxy를 role별로 설정한다. runtime hierarchy에서는 `hostPlayer` / `clientPlayer` name으로 role을 구분한다.
* **ClientToHostPlayerActionIntent**: owner client가 Host에 보내는 action request struct. current first implementation step에서는 `InputFlag.Dash` 와 `InputFlag.Attack` 중 하나만 담고, `ActionSequence / ClientTick / RequestedAction`를 함께 실어 Host validator로 넘긴다.
* **HostPlayerActionValidator**: Host-side action start gate. current first implementation step에서는 controller/state 존재 여부와 `dead / stun / hit / dash cooldown / active attack` rejection rule을 검사하고, accepted/rejected trace를 남기는 기준점 역할을 맡는다.
* **MultiplayerLocalPlayerRegistry**: network spawn 이후 현재 local-owned `PlayerController`를 로컬에서 다시 찾기 위한 static registry. `ThirdPersonCameraController`와 local HUD/gameplay binding이 이 값을 기준으로 다시 붙는다.
* **Remote Display-Only Apply**: remote client가 Host가 복제한 player state/result를 display/HUD 용도로만 반영하고, runtime gameplay FSM apply는 하지 않는 계약. current multiplayer player action authority 문서에서 first implementation step 기본값으로 채택했다.
* **Legacy Scene Player Handoff**: multiplayer gameplay scene이 시작될 때 scene에 남아 있는 old `Player`를 그대로 쓰지 않고, main camera를 먼저 분리한 뒤 spawn snapshot을 캐시하고 runtime hierarchy에서 legacy player를 제거한 다음 network player avatar로 넘기는 handoff 절차. cleanup entry는 `SceneManager.sceneLoaded`와 avatar spawn fallback 두 경로로 보강돼 있다. 2026-04-02 follow-up에서는 verify scene boss가 destroyed legacy player 참조에 남지 않도록 spawned avatar로 boss target을 다시 묶는 rebind path도 함께 들어갔다.
* **Boss Runtime Target Rebind**: multiplayer verify scene에서 boss가 old scene `Player` 대신 spawned network avatar를 다시 추적하게 만드는 runtime handoff. `BossController.SetTarget(...)`와 `MultiplayerGameplaySceneCoordinator` spawn 후 rebind가 한 쌍으로 동작하며, verify path에서 boss attack test가 legacy reference 때문에 끊기지 않도록 한다.
* **Closest Live Player Retarget (가장 가까운 생존 플레이어 재타겟)**: current verify multiplayer temporary rule. `BossController.RefreshClosestLiveTarget(...)`가 non-attack state check에서 현재 씬의 살아있는 `PlayerController` 중 planar distance가 가장 짧은 대상을 다시 고른다. full aggro/contribution system 전의 nearest-target fallback으로 사용한다.
* **Client-Side Boss Authority Disable**: multiplayer client가 local `BossController`와 local `CharacterController`를 함께 끄고, boss gameplay truth를 더 이상 local scene object가 쓰지 않게 만드는 verify safety rule. 2026-04-03 follow-up에서는 이 위에 `Boss Client Presentation Mirror`를 얹어 화면 표시만 Host snapshot으로 복원했다.
* **MultiplayerPlayerPrefabBuilder**: verify gameplay scene의 legacy `Player` subtree를 `Assets/Resources/Multiplayer/MultiplayerPlayerAvatar.prefab`으로 저장하기 위한 editor utility. embedded `Main Camera`는 prefab에서 제거한 뒤 NGO component를 붙이는 용도다.
* **Edit-Mode Title Preview (에디트 모드 타이틀 프리뷰)**: `TitleSceneController`가 `ExecuteAlways` 경로에서 `TitleRuntimeRoot`를 생성/재사용해, Play Mode에 들어가지 않아도 `TitleScene` 패널 배치와 앵커를 Unity Editor에서 바로 조정할 수 있게 하는 처리.
* **Auto Room Title**: Host room title이 비어 있을 때 사용하는 기본 제목 규칙. 형식은 `join here 0000`이며 뒤 4자리는 랜덤 숫자다.
* **Start Unlock Gate**: Host `Start` 버튼이 `2/2 connected` 직후 즉시 열리지 않고 안정 대기 후 열리도록 하는 게이트. 현재 UI 프로토타입에서는 2초 타이머로 표현한다.
* **SimultaneousDeathTest (동시 사망 테스트 컴포넌트)**: `GamePlayScene_TestResult`에서 동일 프레임 사망을 재현하기 위해, `K` 입력 시 플레이어/보스 `Health.TakeDamage`를 같은 프레임에 호출하는 테스트 스크립트(`Assets/Scripts/Test/SimultaneousDeathTest.cs`).
* **동시 사망 판정 우선순위 (GameManager)**: `GameManager.LateUpdate()`가 `_bossDead`를 먼저 검사해 결과를 확정하는 규칙. 플레이어와 보스가 동시에 사망하면 `Victory`를 반환한다.
* **GameOver UI Auto Resolve**: `GameManager`가 `_gameOverRoot` / `_resultLabel` scene binding이 비어 있을 때 `Canvas` 계층에서 `GameOver_Panel`과 `GameResult` TMP를 다시 찾는 fallback 절차. scene-local fileID에 덜 의존하도록 만든다.
* **Magic String (매직 스트링)**: 코드 내에 직접 하드코딩된 문자열 리터럴. 오타 위험이 크므로 `const` 상수로 관리해야 함.
* **Coroutine Cleanup**: `StopAllCoroutines()`를 호출하여 진행 중인 비동기 작업(무적 타이머 등)을 강제 중단하는 기법. 상태 전환(사망 등) 시 잔존 코루틴이 예상치 못한 부작용을 일으키는 것을 방지.
* **Generic State Machine (제네릭 상태 머신)**: `StateMachine<T>` 형태로 구현하여, 플레이어와 보스가 동일한 상태 관리 로직을 공유하면서도 각자의 상태 타입(`PlayerState` vs `BossState`)을 안전하게 사용할 수 있게 하는 기법.
* **Feature Toggle (기능 토글)**: 개발 중인 기능이나 특정 로직(예: 보스 추적, 회전)을 인스펙터 체크박스 하나로 켜고 끌 수 있게 하여, 테스트 효율을 높이고 버그 추적을 용이하게 하는 개발 패턴.
* **Priority Marker (우선순위 마커)**: 작업 목록의 중요도를 일관되게 표시하기 위한 표기 규칙. `🔴(1순위)`, `🟡(2순위)`, `🟢(3순위)` 순으로 관리한다.
* **Task Tag (작업 태그)**: 작업 대상/영역을 괄호로 명시하는 표기 방식. `(플레이어)`, `(보스)`, `(플레이어, UI)`처럼 단일 또는 복수 도메인으로 표기한다.
* **Hook Rule (능동 트리거 규칙)**: 작업 시작 전에 요청 유형을 분류하고, 유형별 선행 문서를 먼저 읽도록 강제하는 운영 규칙.
* **Plan-First Gate (계획 우선 게이트)**: 신규 기능/구조 변경 작업에서 구현 전에 계획서를 먼저 작성하도록 요구하는 절차 규칙.
* **Approval Gate (승인 게이트)**: 계획서에 대해 사용자 승인을 받기 전에는 코드/문서 수정을 시작하지 않는 통제 단계.
* **Agent Role Split (역할 분리 운영)**: 구현 단계는 `Implementer`, 검토 단계는 `QA Reviewer` 역할로 분리해 같은 변경을 서로 다른 관점으로 점검하는 방식.
* **Checklist Update (체크리스트 업데이트)**: Progress_Log에서 완료/진행/보류 작업을 체크박스로 관리해 현재 상태를 빠르게 파악하도록 하는 기록 블록.
* **Context Note (맥락노트)**: 어떤 판단으로 해당 구현을 선택했는지, 어떤 대안을 제외했는지 기록하는 의사결정 메모 블록.
* **Quality Report Triple (품질 보고 3항목)**: 작업 완료 후 `무엇을 발견했는가 / 무엇을 수정했는가 / 왜 그렇게 판단했는가`를 고정 포맷으로 남기는 보고 규칙.
* **Milestone Backlog (마일스톤 백로그)**: 장기 작업 목록(마일스톤, 버그, 폴리싱)을 별도 문서(`docs/roadmap/Milestone_Backlog.md`)에서 단일 책임으로 관리하는 방식.
* **Local Path Link Rule (로컬 경로 링크 규칙)**: 문서/리포트의 파일 링크는 VS Code에서 바로 열리는 로컬 경로 형식으로 작성하고, `file+.vscode-resource.vscode-cdn.net` 웹뷰 URL은 사용하지 않는 규칙.
* **Progress Log Reference File (진행 로그 기준 파일)**: 문서 동기화의 근거로 선택한 일자 로그 파일. 형식은 `docs/Progress_Log/YYYY-MM-DD.md`를 사용한다.
* **Progress Log Tracker Pass (진행 로그 추적 패스)**: `System_Blueprint`/`Technical_Glossary`를 갱신할 때 `Progress_Log`의 항목(`오늘 반영한 작업`, `체크리스트 업데이트`, `맥락노트`, `기술적 고려`)을 근거로 매핑 검증하는 절차.
* **Sync Trace Note (동기화 추적 메모)**: 완료 보고에 남기는 근거 문장. 권장 형식은 `참조 로그: docs/Progress_Log/YYYY-MM-DD.md`.
* **6-Line Trace Card (6줄 추적 카드)**: 코드 읽기/버그 분석 시 동작 1개를 `Trigger -> Entry -> Gate -> Core Check -> Effect -> Result` 6단계로 고정 기록하는 규칙.
* **Trace Line Format (추적 라인 형식)**: 카드 각 줄을 `[S#] Action | Condition | File:line | Key value` 형태로 쓰는 기록 포맷.
* **Single-Behavior Scope (단일 동작 범위)**: 한 Trace Card에 서로 다른 기능을 섞지 않고, 하나의 동작(예: Attack4 AoE hit)만 다루는 원칙.
* **Field-First Member Layout (필드 우선 멤버 배치)**: 클래스 가독성과 유지보수를 위해 직렬화/런타임 필드를 먼저 선언하고, `OnValidate` 등 메서드는 필드 선언 이후에 배치하는 구성 규칙.
* **Planar Distance Gate (평면 거리 게이트)**: Boss의 상태 전환 거리 판정에서 높이(Y)를 제외하고 XZ 평면 거리만 사용해 점프/지형 높이 차로 인한 오판정을 줄이는 규칙.
* **Pattern Attack Range (패턴별 공격 사거리)**: `BossController`가 공격 패턴마다 별도 사거리(`Basic`, `Lunge`, `Projectile`, `AoE`)를 가지는 규칙. 공격 패턴 선택 시 현재 거리에서 유효한 패턴만 후보로 포함한다.
* **Basic Range Origin (기본 공격 사거리 기준점)**: Basic 패턴 거리 판정의 시작점 Transform. `basicAttackRangeOrigin`으로 주입하며, 미할당 시 Boss Root를 사용한다. 현재 기본 씬에서는 `HeadDamageCasterPlace`를 기준점으로 사용한다.
* **Basic Range Single Source (기본 사거리 단일 source)**: Attack1의 조정 가능한 사거리 값은 `HeadDamageCaster.radius` 하나만 사용한다. `BossController.BasicAttackRange`와 Basic gizmo는 이 값을 읽어 공격 가능 거리 판단과 실제 타격 판정 반경이 같은 source를 공유한다. 숨겨진 `basicAttackRange` 필드는 `HeadDamageCaster` 미할당 시 legacy fallback으로만 남긴다.
* **Attack1 Ready Window (Attack1 준비동작 윈도우)**: Attack1에서 실제 bite 판정이 열리기 전에 유지되는 준비 구간. `BasicAttackSettings.readyNormalizedWindow`로 bite 애니메이션의 어느 slice를 ready motion으로 쓸지 정하고, `HeadDamageCaster`는 이 구간이 끝날 때까지 비활성 상태를 유지한다.
* **Attack1 Ready Duration (Attack1 준비 시간)**: `BasicAttackSettings.readyDuration` 값. 선택한 ready slice가 몇 초 동안 재생될지 정하며, 구현은 `Animator.speed` 임시 보정으로 해당 구간의 재생 시간을 다시 맞춘다.
* **Logic-Owned DamageCaster (로직 소유 DamageCaster)**: `DamageCaster` 컴포넌트를 Boss root 또는 로직 전용 자식에 두고, 공격 시점/Owner/HitType은 `BossController`와 패턴 로직이 관리하는 배치 방식. Visual hierarchy에는 위치 추종용 앵커만 남긴다.
* **Visual Cast Anchor (비주얼 판정 앵커)**: `HeadDamageCasterPlace`, `BodyDamageCasterPlace`처럼 Visual/Bone 계층에 남겨두는 위치 기준 Transform. `DamageCaster`는 이 앵커를 `_castCenter`로 사용해 본 이동만 추종한다.
* **Phase1 Attack Priority (페이즈1 공격 우선순위)**: Phase1에서 Basic/Lunge 사거리 조건이 동시에 성립하면 Basic을 우선 선택하는 규칙. Lunge는 Basic 범위를 벗어난 경우에만 선택한다.
* **Lunge Root Motion Relay (도약 루트모션 릴레이)**: Lunge 애니메이션의 루트모션 델타를 Animator `OnAnimatorMove`에서 수신해 `BossController.ApplyLungeRootMotion(deltaPosition, normalizedTime)`으로 전달하는 방식. 기본 경로는 Animator XZ 델타 기반이며, 캐릭터 루트 이동은 `CharacterController.Move`로 적용된다.
* **Lunge Root Motion Fallback (도약 루트모션 폴백)**: `animator.deltaPosition` XZ 델타가 임계값 이하일 때 `Visual` 월드 이동량 델타를 대체 입력으로 사용하는 보조 경로.
* **Visual Local Pose Restore (비주얼 로컬 기준점 복원)**: Lunge 시작 시점의 자식 `Visual` 로컬 위치/회전을 캐시한 뒤, 루트모션 적용 프레임과 종료 시점에 기준 포즈로 복원해 부모 루트와 자식 비주얼 좌표 불일치 누적을 차단하는 방식.
* **Lunge Travel Direction Lock (도약 이동 방향 고정)**: Lunge 시작 시 타겟 방향을 고정하고, 루트모션 이동량을 해당 방향으로 재투영하는 규칙.
* **Lunge Motion Distribution Tuning (도약 이동량 분포 보정)**: 실험 9에서 `normalizedTime` 구간별 배수(`midBoost*`, `lateReduce*`)로 Lunge 이동량 분포를 조정하는 방식. `enableMotionDistributionTuning`으로 토글하며, `OnValidate`에서 구간/배수 유효성을 보정한다.
* **Lunge Root Motion Scale Probe (도약 루트모션 배수 프로브)**: 실제 프레임에 적용된 이동량 배수를 `LastLungeRootMotionMagnitudeScale`과 `[LungeDebug][RootMotion] scale=...` 로그로 노출해 튜닝값 반영 여부를 추적하는 기록 항목.
* **Lunge Damage Window Timing (도약 판정 윈도우 타이밍)**: 현재 Lunge 패턴은 `damageCastNormalizedWindow`의 `start/end` 값을 사용해 Attack2 판정이 열리는 구간을 제어한다. 상태 종료 시점은 `normalizedTime 1.0`을 유지한다.
* **Range-Only Detection Trigger (거리 단일 감지 트리거)**: Idle/Searching에서 Combat(스크림 인트로) 진입을 감지 반경(`IsTargetInDetectionRange`)만으로 판정하는 규칙. 장애물/시야선(LOS) 여부와 무관하게 거리 조건만 충족하면 전투 전환이 발생한다. current verify multiplayer follow-up에서는 이 판정 전에 `Closest Live Player Retarget`이 먼저 target을 최신 nearest player로 맞춘다.
* **Chase Hysteresis (추적 히스테리시스)**: 단일 공격 사거리 임계값 대신 현재 페이즈에서 활성화된 패턴의 `최대 사거리`(해제)와 `최대 사거리 + ChaseReengageBuffer`(재진입) 이중 임계값을 두어 Walk/Idle 경계 지터를 완화하는 기법.
* **Asset+Meta Pair Rule (에셋-메타 쌍 규칙)**: Unity 에셋은 파일만 커밋하면 참조가 보장되지 않는다. 참조 안정성을 위해 원본 에셋과 해당 `.meta`를 반드시 쌍으로 버전관리하는 규칙.
* **Dependency Closure Tracking (의존성 폐쇄 추적)**: 특정 씬/프리팹이 참조하는 직접/간접 에셋을 그래프 형태로 확장해 누락 없이 추적 세트를 산출하는 방식.
* **Selective LFS Tracking (선별 LFS 추적)**: 2026-03-24 Google Drive cutover 이전에 사용하던 구형 운영 방식. 대용량 에셋 전체를 일괄 추적하지 않고, GUID 의존성 폐쇄로 계산한 실사용 런타임 에셋만 Git LFS 대상으로 제한했다.
* **Runtime Required Asset Set (실행 필수 에셋 세트)**: 현재 트래킹된 씬/프리팹/설정에서 실제로 참조되는 에셋의 직접+간접 의존성 집합. 크로스 PC 재현성을 보장하기 위한 최소 버전관리 단위로 사용한다.
* **GUID Orphan Reference (GUID 고아 참조)**: YAML에 남아 있는 GUID가 로컬/레포 어디에도 존재하지 않아 `Missing`으로 해석되는 참조 상태.
* **Manual Import Baseline (수동 임포트 기준선)**: 저장소 용량 제약이 있을 때 대용량 서드파티 에셋은 Git에서 제외하고, 팀원이 동일 버전을 수동 임포트해 작업 기준선을 맞추는 운영 규칙.
* **Google Drive Third-Party Pack Baseline (구글 드라이브 서드파티 팩 기준선)**: imported third-party asset pack을 Git/LFS 대신 Google Drive zip으로 배포하고, 팀원이 zip을 같은 `Assets/...` 경로에 풀어 로컬 기준선을 맞추는 운영 규칙. zip에는 원본 에셋과 `.meta`를 함께 포함한다.

## 4. Optimization (Performance)

* **Zero-GC (제로 GC)**: 런타임 중에 가비지 컬렉터가 작동하지 않도록 힙(Heap) 메모리 할당을 0에 가깝게 유지하는 설계 원칙.
* **Non-Alloc API**: 유니티 엔진 기능 중 결과값을 새로운 배열로 생성(`new`)하지 않고, 미리 할당된 배열에 채워 넣어주는 API.
    *   **VS Alloc (`Physics.OverlapSphere`)**: 호출할 때마다 매번 `Collider[]` 배열을 새로 생성(Allocation)하여 힙 메모리를 사용함. 프레임마다 호출하면 GC Spaike(랙)의 주범이 됨.
    *   **VS NonAlloc (`Physics.OverlapSphereNonAlloc`)**: 미리 만들어둔 배열(`pre-allocated array`)을 재사용함. 메모리 할당이 전혀 발생하지 않음(Garbage Free). 단, 배열 크기(`_maxTargets`) 이상의 충돌체는 감지하지 못하므로 크기 설정에 주의 필요.
* **Object Pooling**: 투사체나 이펙트를 파괴(Destroy)하지 않고 비활성화 후 재사용하여 CPU 부하를 줄이는 관리 방식.
* **Compound Collider (복합 충돌체)**: 하나의 무거운 Mesh Collider 대신, 여러 개의 가벼운 Primitive Collider(Box, Sphere, Capsule)를 조합하여 복잡한 형태의 충돌을 효율적으로 처리하는 기법. 보스의 부위별 피격 판정에 사용됨.

## 5. Combat System

* **Hitbox (히트박스)**: 공격 판정이 발생하는 가상의 구체 또는 박스 영역.
* **Hurtbox (허트박스)**: 피격 판정이 발생하는 영역. 캐릭터의 충돌체와 일치하거나 약간 작게 설정함.
* **Frame-based Detection**: 애니메이션의 특정 프레임 혹은 짧은 시간 동안만 물리 체크를 활성화하여 판정하는 방식.
* **Input Buffer (선입력)**: 애니메이션 종료 직전에 입력된 명령을 저장해두었다가, 동작 가능 시점에 즉시 실행하여 조작감을 향상시키는 시스템.
* **Animation Cancel (모션 캔슬)**: 현재 진행 중인 동작(특히 후딜레이)을 중단하고 대시 등의 긴급 회피 동작으로 즉시 전환하는 기법.
* **IDamageable**: 대상을 특정하지 않고 데미지 명령(`TakeDamage`)만 내릴 수 있게 해주는 추상화 인터페이스.
* **IBossAttackHitReceiver**: 보스 공격의 종류/힘 방향 메타데이터(`BossAttackHitData`)를 수신해, 대상(현재는 플레이어)이 피격 반응(일반 피격/스턴/무시)을 직접 판정하도록 하는 인터페이스. current multiplayer step `4~6`에서는 이 결과가 `BossAttackResolved` event를 통해 `HostPlayerReactionResolver`로도 전달된다.
* **BossAttackHitType**: 보스 공격 분류 열거형. `Attack1`, `Attack2`, `Attack3Projectile`, `Attack4Projectile`로 피격 처리 규칙을 분기한다.
* **Warning Phase (경고 구간)**: 공격이 실제로 적용되기 전, 원/이펙트로 위험 구역을 보여주는 준비 구간. 현재 코드에서는 `warningDuration`으로 시간을 설정한다.
* **Warning Single Source Sync**: Attack4에서 경고 종료와 fire 착지 타이밍을 각각 따로 설정하지 않고, `warningDuration` 하나로 동시에 제어하는 규칙.
* **Attack4 Fully-Red Active Window**: AoE circle이 warning을 끝내고 fully red가 된 뒤 유지되는 판정 구간. 이 구간에서 반경 내 대상에게만 피해를 적용한다.
* **Fallback Radius Scale Multiplier**: AoE runtime fallback 디스크 반경 스케일 배수. 현재 기본값 `1.2`는 물리 데미지 반경보다 경고 시각을 약간 크게 보여 UX 불공정 체감을 줄이기 위한 보수적 설정이다.
* **Circle One-Hit Registry (`HashSet<int>`)**: AoE circle 단위로 이미 타격한 대상 ID를 기억하는 집합. 같은 circle에서 같은 대상은 1회만 맞도록 보장한다.
* **Invul Ignore Non-Consume**: `ReceiveBossAttackHit` 결과가 `Ignored`일 때 그 시도를 hit consumed로 확정하지 않는 규칙. invul이 끝나면 active window 안에서 1회 피격 기회를 다시 평가한다.
* **Projectile Count Timer**: 플레이어가 투사체 피격 누적을 판정하는 짧은 타이머 창. 타이머 내 1타는 일반 피격, 2타 이상은 스턴으로 승격한다.
* **StunState**: 플레이어 스턴 전용 상태. 입력 기반 행동(이동/공격/스킬/점프/상호작용/회전)을 차단하고, 피격 방향 기반 푸시백과 스턴 타이머를 처리한다.
* **Damage-Reaction Split (데미지-반응 분리)**: HP 감소 처리와 상태 반응(`HitState`, `StunState`)을 분리하는 규칙. Attack2처럼 `damage + stun` 조합이 필요한 보스 공격에서 공용 데미지 파이프라인을 재사용할 수 있다.
* **Post-Stun Invulnerability**: 스턴 종료 후 적용되는 후속 무적 구간. 데미지/재스턴을 차단하며, `BlinkWhiteEffect`가 점멸 표현(기본 주기 0.2s)을 담당한다.
* **BlinkWhite Shader Parameter (`_BlinkWhite`)**: 플레이어 후속 무적 점멸 표현을 위한 전용 셰이더 파라미터. `0`은 원본 색, `1`은 흰색으로 해석하고 `lerp(originalBaseColor, white, _BlinkWhite)` 규칙으로 최종 출력색을 만든다. 현재 구현에서 `1` 구간은 조명 영향을 무시한 순수 white 출력이다.
* **BlinkWhiteEffect**: 플레이어/보스에 부착 가능한 점멸 전용 컴포넌트(`Assets/Scripts/Common/Visual/BlinkWhiteEffect.cs`). `_BlinkWhite` 셰이더 파라미터를 `MaterialPropertyBlock`으로 제어하며, `PlayBlink`, `PlaySingleBlink`, `SetBlink`, `StopBlink` API를 제공한다.
* **Runtime Blink Material Swap**: 렌더러의 원본 머티리얼에 `_BlinkWhite`가 없을 때, 런타임에서 Blink 셰이더(`Assets/Shaders/BlinkWhiteLit.shader`)를 사용하는 복제 머티리얼 세트를 준비/활성화하는 절차. 효과 종료 시 원본 머티리얼로 복구한다.
* **Boss Hit Motion Suppression**: 보스가 공격 준비/실행 상태일 때 피격 모션(`BossHitState`) 전환을 무시하는 규칙. 이때도 `BlinkWhiteEffect` 기반 피격 점멸은 정상 재생된다.
* **Attack Window Result Event**: 공격 판정 시작(`EnableHitbox`)부터 종료(`DisableHitbox`)까지 누적된 결과를 1회 발행하는 이벤트. 현재 `DamageCaster.OnAttackWindowResolved(bool isHit, int totalDamage)`로 구현되어 HUD 피드백과 Host-side `raw hit log` 기록 트리거에 함께 사용된다.
* **Attack Hit Confirm Event**: 공격 윈도우 도중 첫 번째 valid hit가 확인되는 즉시 발행되는 이벤트. 현재 `DamageCaster.OnAttackHitConfirmed`로 구현되어 combo UI를 miss와 분리해 열 때 사용한다.
* **Fixed Damage Feedback (고정형 데미지 피드백)**: 월드 위치를 추적하지 않고 HUD의 고정 앵커에서 `HIT + 피해량`만 표시하는 피드백 방식. 현재는 적중 시 확대 후 짧은 페이드 아웃으로 마무리한다.
* **Ghost Hitbox Guard (잔존 히트박스 가드)**: 상태 전환/초기화 시 `ForceDisableHitbox()`와 `AttackState.Exit()`를 통해 공격 판정이 남지 않도록 강제 정리하는 보호 규칙.
* **Zero-Damage Filter (무데미지 필터)**: `DamageCaster.EnableHitbox`와 `Health.TakeDamage`에서 0 이하 데미지를 무시해 피격 이벤트/애니메이션 오작동을 방지하는 안전 장치.
* **Animation Event Bridge**: 애니메이터의 타임라인 이벤트를 코드 로직(`PlayerController` 등)으로 연결해주는 중계 클래스.
* **IBossAttackPattern**: 보스 공격 패턴 인터페이스 (Strategy Pattern 적용). `Enter`/`Update`/`Exit` 메서드를 정의하여 `BossAttackState`가 구체 패턴을 몰라도 실행할 수 있게 함.
* **BasicAttackPattern**: `IBossAttackPattern`의 기본 구현체. 보스의 근접 공격에서 bite 애니메이션 재생, ready slice 재생 시간 보정, 준비 구간 종료 후 `HeadDamageCaster` 오픈, `normalizedTime 1.0` 기준 종료를 담당한다.
* **Animator Playback Speed Override (애니메이터 재생 속도 오버라이드)**: 특정 공격 구간의 재생 시간을 맞추기 위해 `Animator.speed`를 임시로 변경하는 처리. Attack1 준비동작에서는 ready slice 동안만 적용하고, 상태 종료/인터럽트 시 반드시 `1.0`으로 복구한다.
* **Invincibility Frame (무적 시간)**: 특정 구간 동안 추가 데미지를 차단하는 보호 기간. 현재 플레이어는 `stunned` 또는 `post-stun invulnerability` 상태에서 `Health.SetInvincible(true/false)`로 제어한다.
* **Bone-Synced Hitbox (본 동기화 피격 판정)**: `DamageCaster._castCenter`를 스켈레톤의 Bone 자식 Transform으로 설정하여, 애니메이션에 따라 히트박스 위치가 자동으로 동기화되는 기법. `DamageCaster` 컴포넌트 자체는 로직 계층에 두고, 위치만 Bone 앵커를 따라가게 구성할 수 있다.
* **Partial Animation (부분 애니메이션)**: 애니메이션 클립 전체를 재생하지 않고, 특정 구간(예: 도약 부분)만 재생한 후 강제로 종료(`exitPhaseRatio`)하여 동작의 템포를 조절하는 기법. 복귀 모션 등을 생략하여 타격감을 높일 때 사용됨.
* **Lunge Hitbox/Exit Split Timing (도약 판정/상태 종료 분리 타이밍)**: Lunge 패턴에서 히트박스 시작/종료는 `damageCastNormalizedWindow`로 조정하고, 상태 종료는 별도로 `normalizedTime 1.0` 기준으로 유지하는 방식. 이동은 루트모션 릴레이가 담당한다.
* **Windup (준비 구간)**: Attack2 시작 직후의 사전 동작 구간. 본 구간에서는 보스가 도약을 시작하기 전 지면 정렬과 충돌 안정화가 우선된다.
* **PreLaunch (도약 직전 구간)**: 실제 이륙(Launch) 직전의 마지막 준비 구간. `Windup`과 함께 ground lock 적용 대상이며, 플레이어 머리 타기 회귀를 막기 위해 `stepOffset 0`을 유지한다.
* **Launch Marker (도약 시작 마커)**: Attack2에서 ground lock을 해제하고 루트모션 Y를 허용하는 전환 기준 이벤트(애니메이션 마커). 월드 Y 변화량 대신 상태 전환의 기준점으로 사용한다.
* **Attack2 Marker Fallback (Attack2 마커 폴백)**: 애니메이션 이벤트(`PreLaunchStart/Launch/Land`)가 누락되거나 타이밍이 밀릴 때 `normalizedTime` 임계값으로 동일한 구간 전이를 보장하는 보조 규칙.
* **Attack2 Marker Queue (Attack2 마커 큐)**: Attack2 애니메이션 마커를 단일 슬롯이 아닌 큐로 누적해 프레임 드랍/동시 이벤트 상황에서 마커가 덮어써져 유실되는 문제를 방지하는 처리.
* **AnimEventSynth (합성 마커 이벤트)**: AnimEvent가 누락된 Attack2에서 `BossRootMotionRelay`가 정규화 시간 임계값을 기준으로 `PreLaunchStart/Launch/Land` 마커를 직접 큐잉하는 보조 경로.
* **MarkerPathWarn (마커 경로 경고 로그)**: 기대된 Attack2 애니메이션 마커가 수신되지 않아 폴백 경로(`normalizedTime`)로 전이됐음을 강제 로그로 기록하는 추적 태그.
* **Ground Lock (지면 잠금)**: `Windup/PreLaunch` 동안 Ground 레이어 Raycast로 목표 Y를 계산해 보스 바닥 높이를 강제 유지하는 보정 기법. 루트모션은 XZ만 반영하고 Y는 고정한다.
* **Attack2 Launch Guard (Attack2 Launch 상한 가드)**: `launchNormalizedTime`/`landSnapNormalizedTime`을 종료 시점 이전 상한(`0.98`)으로 강제해 Launch 영구 미진입 설정을 예방하는 규칙.
* **Animator Delta Follow (애니메이터 델타 추종)**: Attack2 이동에서 `CharacterController`가 자체 이동 벡터를 계산하지 않고 Animator `deltaPosition`을 그대로 `Move`에 반영해 애니메이션 루트 이동을 직접 추종하는 방식.
* **StepOffset Zeroing (스텝 오프셋 제로잉)**: Attack2 준비 구간에서 `CharacterController.stepOffset`을 `0`으로 내려 자동 계단 오르기를 차단하는 처리. 플레이어 머리/어깨를 계단처럼 인식해 올라타는 문제를 예방한다.
* **Attack2 Repro Harness (Attack2 재현 하네스)**: `GamePlayScene_TestResult`에서 플레이어를 보스 전방 재현 위치로 자동 고정해, 수동 위치 조정 없이 Attack2 위로 올라감 회귀를 반복 재현하는 테스트용 보조 컴포넌트.
* **Attack2 Landing Debug Log (Attack2 착지 분석 로그)**: 착지 순간의 턱 걸림/지연을 진단하기 위해 `[Attack2Landing]` 프리픽스로 출력하는 추적 로그 체계. RootMotion 델타, Gravity 이동량, GroundSnap 결과, `CollisionFlags(Sides/Above/Below)`, 애니메이션 마커 소비 시점을 함께 기록한다.
* **RootMotionRelayProbe (루트모션 릴레이 프로브)**: `BossRootMotionRelay.OnAnimatorMove` 단계에서 `normalizedTime`, `animator.deltaPosition`, `applyRootMotion` 상태를 강제 출력해 루트모션 입력 경로 유효성을 검증하는 로그 태그.
* **Attack2 SpatialProbe (Attack2 공간 좌표 프로브)**: `[Attack2Landing][SpatialProbe]` 로그로 `player`, `boss`, `visual`, `red(Boss/Visual/Red)` 월드 좌표와 상대 벡터(`player-boss`, `player-red`, `boss-red`, `visualInBoss`, `redInBoss`) 및 `redPath`를 함께 출력해 좌표계 이탈 원인을 추적하는 규칙.
* **GroundSnap Failure Reason (GroundSnap 실패 사유 코드)**: `GroundSnapMiss`/`GroundSnapSkipMaxDistance` 로그에 `InvalidSetup`, `MaskEmpty`, `NoHit`, `AllFiltered` 같은 원인 코드를 명시해 착지 보정 실패의 원인을 즉시 판별하는 규칙.
* **Attack2 Core Timing Trio (Attack2 핵심 타이밍 3종)**: `preLaunchStartNormalizedTime`, `launchNormalizedTime`, `landSnapNormalizedTime`의 묶음. Windup/PreLaunch/Airborne/LandSnap 구간 경계를 정의하는 1차 튜닝 축이다.
* **Attack2 Inspector Damage Window Gauge (Attack2 인스펙터 피격 윈도우 게이지)**: `damageCastNormalizedWindow`를 기본 인스펙터에서 2핸들 MinMax 슬라이더와 `Start/End` float field로 표시하는 UX. Attack2 DamageCaster 활성 구간을 `normalizedTime 0~1` 범위에서 직접 튜닝한다.
* **Attack1 Inspector Ready Gauge (Attack1 인스펙터 준비 게이지)**: `readyNormalizedWindow`를 기본 인스펙터에서 2핸들 MinMax 슬라이더와 `Start/End` float field로 표시하는 UX. Attack1 bite 애니메이션 안에서 어떤 구간을 ready motion으로 쓸지 `normalizedTime 0~1` 범위로 직접 튜닝한다.
* **Attack2 Player Y Trace (`[Attack2PlayerY]`)**: 플레이어가 Attack2 근접/피격/스턴 이동 구간에서 출력하는 디버그 로그 태그. `playerY`, `bottomY/topY`, `grounded`, `ccVelY`, `CollisionFlags`, 현재 상태, 보스 Attack2 거리/정규화 시간을 함께 기록해 하반신 잠김 원인을 추적한다.
* **Attack2 Gizmo Feature Toggle Set (Attack2 기즈모 기능 토글 세트)**: `showAttackRangesGizmo`, `showDetectionRangeGizmo`, `showAttack2GroundProbeGizmo`, `showAttack2SnapWindowGizmo`, `showAttack2SpatialLineGizmo`의 묶음. 필요한 진단 기즈모만 선택 출력해 디버깅 노이즈를 줄이는 규칙이다.
* **Ground Probe Gizmo (지면 프로브 기즈모)**: Attack2의 `groundRayStartHeight`, `groundRayDistance`, `groundMask` 기준으로 실제 Ray 시작점/길이/히트 지점을 Scene 뷰에 표시하는 시각 진단 도구.
* **Snap Window Gizmo (스냅 윈도우 기즈모)**: `groundSnapMaxDistance`와 `groundSnapEpsilon`을 시각화해 현재 Boss Y 대비 허용 보정 범위와 목표 스냅 Y가 창 안에 있는지 표시하는 디버그 기즈모.
* **Recovery Baseline Rollback (회복 기준선 회귀)**: 다층 튜닝(파라미터/디버그/시각화) 이후에도 핵심 증상이 남을 때, 최근 실험을 덧붙이지 않고 마지막 안정 커밋의 이동 경로로 먼저 되돌리는 복구 전략.

## 6. Animation System

* **Animator Controller**: Unity의 애니메이션 상태 머신. FSM과 연동하여 상태 전환 시 애니메이션을 재생함.
* **CrossFade**: 현재 애니메이션에서 목표 애니메이션으로 부드럽게 블렌딩하는 Unity Animator 메서드. 끊김 없는 전환을 위해 사용.
* **Blend Tree**: 하나의 파라미터(예: `Speed`)에 따라 여러 애니메이션을 자동으로 섞어 재생하는 구조. Idle↔Run 전환에 사용.
* **Motion GUID Drift (모션 GUID 드리프트)**: Animator State 이름은 유지되지만, 참조 중인 AnimationClip GUID가 유실/변경되어 `Motion Missing`이 발생하는 상태.
* **Animator Motion Rebinding (모션 재바인딩)**: 유실된 Motion 참조를 현재 프로젝트에 존재하는 FBX/Clip의 `guid + fileID`로 다시 연결해 상태를 복구하는 작업.
* **PlayerAnimator Guard**: `Assets/Editor/PlayerAnimatorGuard.cs`가 필수 상태/모션, 필수 파라미터(`Speed` Float, `Hit` Trigger), Locomotion BlendTree 자식 모션을 자동 점검하는 안전 장치. 모든 Layer + 중첩 StateMachine 재귀 순회와 중복 상태명 경고를 포함하며, `Hit` 상태명은 `PlayerController.ANIM_STATE_HIT` 상수를 공용 참조한다.
* **Animator Parameter Contract (애니메이터 파라미터 계약)**: 컨트롤러가 반드시 보유해야 하는 파라미터 이름/타입 약속. 현재 플레이어는 `Speed: Float`, `Hit: Trigger`를 계약으로 고정해 가드 스크립트로 검증한다.
* **Environment Bug Auto-Fix (환경 변경 버그 자동 복구)**: 환경 변경/재임포트 과정에서 공격 클립(`Attack1/2/3`) 이벤트가 유실되거나 틀어진 경우, 에디터 가드가 preset 타이밍으로 `OnHitStart/OnHitEnd`를 자동 삽입/정렬해 런타임 판정 버그를 예방하는 기능.
* **Environment Bug Validation (환경 변경 버그 검증)**: 환경 변경 이후 `OnHitStart`/`OnHitEnd` 누락 또는 순서 오류를 검사해 에디터에서 즉시 에러로 표시하는 검증 규칙. `Tools/Validation/Fix Player Attack Events` 메뉴로 수동 복구도 지원한다.
* **Boss Attack Priority**: `BossCombatState.Update()`는 공격 패턴 진입이 가능하면 `MoveTo`/`PlayMove`와 같은 추적 이동 호출보다 `AttackState` 전환을 우선 적용한다.
* **Package Baseline Rollback (패키지 기준선 롤백)**: Unity 버전 복귀 시 `ProjectVersion`만 변경하면 패키지 그래프가 어긋날 수 있으므로, `manifest` 정규화와 `packages-lock` 재생성을 함께 수행해 의존성 해석 오류를 제거하는 복구 절차.
* **Locomotion Visual Suppression (이동 시각 잠금)**: AoE 공중 패턴처럼 비행 애니메이션이 우선이어야 할 때 `MoveTo`/`StopMoving`가 `PlayMove`/`PlayIdle`를 강제하지 않도록 막아 Walk가 `TakeOff/Fly`를 덮어쓰지 못하게 하는 보호 계층.
* **FlyForward Fallback (비행 전진 폴백)**: `FlyForward` 상태가 Animator에 없을 때 지상 `Locomotion`으로 떨어지지 않고 `FlyIdle`로 폴백해 공중 연출을 연속 유지하는 보정 규칙.
* **Post-Fire Recovery Window (발사 후 복귀 윈도우)**: Projectile 패턴에서 마지막 발사 직후 곧바로 Combat으로 복귀하지 않고 최소 대기(`postFireRecoveryDuration`) 및 애니메이션 진행률(`exitNormalizedTime`) 조건을 만족할 때 전환하는 안정화 구간.
* **Exit Normalized Time (종료 정규화 시점)**: 공격 애니메이션이 어느 진행률에서 종료 판정을 허용할지 정의하는 기준값. `AnimatorStateInfo.normalizedTime`과 비교해 패턴 복귀 타이밍을 제어한다.
* **URP Global Settings Regeneration (URP 글로벌 설정 재생성)**: 패키지 버전 전환이나 GUID 드리프트로 `UniversalRenderPipelineGlobalSettings.asset` 참조가 깨졌을 때, Unity 에디터에서 글로벌 설정 자산을 재생성/재할당해 참조 정합성을 회복하는 절차.

## 7. Design Patterns

* **Strategy Pattern (전략 패턴)**: 알고리즘(행동)을 인터페이스로 추상화하여 런타임에 교체 가능하게 하는 디자인 패턴. 본 프로젝트에서는 `IBossAttackPattern`으로 보스 공격 패턴을 교체 가능하게 구현함.
---

## Boss Phase Addendum (2026-02-20)

- **Boss Phase**: 보스 전투를 구간별로 분리해 공격 풀과 행동 규칙을 다르게 적용하는 상태.
- **Phase Intro (Scream)**: 각 페이즈 시작 시 1회 재생되는 전환 연출. 재생 중 공격 선택을 잠시 잠근다.
- **Phase Attack Window**: 페이즈 인트로가 끝난 뒤 실제 공격 패턴 선택이 허용되는 구간.
- **No-Immediate-Repeat Selector**: 두 패턴이 모두 활성일 때 직전 패턴과 동일한 패턴을 연속 선택하지 않도록 하는 선택 규칙.
- **HealthRatio**: `CurrentHealth / MaxHealth` 값. 보스의 페이즈 전환 임계치 판정(예: 0.5)에 사용.



## Unity Compatibility Addendum (2026-02-20)
- **Editor Assembly Anchor (에디터 어셈블리 앵커)**: `Assets/Editor`에 최소 1개 스크립트를 유지해 `Assembly-CSharp-Editor` 생성이 보장되도록 하는 안정화 패턴.
- **Unity API Drift (API 드리프트)**: Unity 버전 전환 시 동일 기능의 프로퍼티/메서드 시그니처가 달라져 발생하는 호환성 문제. 본 프로젝트에서는 `Rigidbody.linearVelocity` -> `Rigidbody.velocity` 교체로 복구.
* **Owner Render Proxy (owner 화면용 render proxy)**: owner free move 화면에서 raw predicted root 대신 body와 camera가 같이 보는 부드러운 presentation 기준점. gameplay truth/root/collider는 raw network tick state를 유지하고, 화면 표시만 이 proxy를 통해 완화한다. current follow-up에서는 predicted owner camera도 이 proxy를 direct orbit follow로 본다.
