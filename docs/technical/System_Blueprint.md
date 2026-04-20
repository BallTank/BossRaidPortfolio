# 🛠️ System Blueprint: Boss Raid Portfolio

이 문서는 프로젝트의 핵심 아키텍처 설계와 데이터 규칙을 정의합니다. AI 및 개발자는 이 청사진을 준수하여 코드를 작성해야 합니다.

## 1. Core Architecture Philosophy
* **Decoupling (탈응집)**: 입력(Provider) → 해석(Controller) → 행동(State)의 단방향 의존성 유지.
* **Network-Ready Data**: 로직에는 `bool`이나 `Input` 클래스를 직접 사용하지 않고, 반드시 직렬화 가능한 `PlayerInputPacket` 구조체만 전달한다.
* **Zero-GC**: `Update` 루프 내에서의 메모리 할당(new)을 금지하며, 구조체(Struct)와 NonAlloc 물리 API를 사용한다.

---

## 2. Technical Class Diagram (Target Architecture)
본 프로젝트는 `StateMachine` 패턴을 기반으로 Player와 Boss의 로직을 제어합니다.

### 2.1. Player System Architecture
```mermaid
classDiagram
    direction TB

    %% Interfaces
    class IInputProvider {
        <<Interface>>
        +GetInput() PlayerInputPacket
    }
    class IDashContext {
        <<Interface>>
        +StartDashCooldown()
    }
    class IAttackable {
        <<Interface>>
        +AttackCombos AttackComboData[]
    }
    class IDamageable {
        <<Interface>>
        +TakeDamage(int)
    }
    class IBossAttackHitReceiver {
        <<Interface>>
        +ReceiveBossAttackHit(BossAttackHitData) BossAttackHitResolution
    }

    %% Data
    class PlayerInputPacket { <<Struct>> }
    class AttackComboData { <<Struct>> }
    class InputFlag { <<Enumeration>> }
    class ClientToHostPlayerActionIntent { <<Struct>> }
    class HostPlayerState { <<Struct>> }
    class HostToClientPlayerReactionSnapshot { <<Struct>> }
    class MultiplayerLocomotionState { <<Struct>> }
    class BossAuthoritativeState { <<Struct>> }
    class BossAuthoritativeLocomotionState { <<Enumeration>> }
    class BossAuthoritativeAttackId { <<Enumeration>> }
    class BossAuthoritativeAttackVisualState { <<Enumeration>> }
    class BossAuthoritativePhase { <<Enumeration>> }
    class BossAttackHitData { <<Struct>> }
    class BossAttackHitType { <<Enumeration>> }
    class BossAttackHitResolution { <<Enumeration>> }

    %% Core Components
    class PlayerController {
        <<MonoBehaviour>>
        +MoveSpeed float
        +Animator Animator
        -_stateMachine StateMachine
        -_multiplayerPresentationDriver MultiplayerPlayerPresentationDriver
        +MoveState MoveState
        +DashState DashState
        +JumpState JumpState
        +AttackState AttackState
        +HitState HitState
        +StunState StunState
        +DeadState DeadState
        +PlayerVisual Visual
        +Update()
        +SetCameraRoot(Transform)
        +GetAttackFacingDirection() Vector3
        +SetActionAuthorityMode(...)
        +CaptureCurrentLocomotionState(...)
        +ApplyLocomotionState(...)
        +SimulateLocomotionTickFromCurrent(...)
        +TryStartAuthoritativeAttackComboStep(...)
        +TryStartAuthoritativeDash(...)
        +TryStartAuthoritativeAttack1(...)
        +ApplyAuthoritativeAttackHudFeedback(int, int)
        +ApplyAuthoritativeReactionSnapshot(...)
        +RefreshLocalPresentationBindings()
        +GetPreferredCameraFollowPosition() Vector3
        +SetLocomotionAnimatorSpeed(...)
        +SetPendingComboHudStep(int)
        +ShowComboHud(int)
        +HideComboHud()
    }

    class BaseState {
        <<Abstract>>
        +Enter()
        +Exit()
    }

    class PlayerBaseState {
        <<Abstract>>
        +Update(PlayerInputPacket)*
    }

    class LocalInputProvider { +GetInput() }
    class MultiplayerBufferedInputProvider {
        +SetInput(PlayerInputPacket, int)
        +Clear()
        +GetInput()
    }
    class PlayerLocomotionCore {
        <<Static>>
        +CaptureCurrentState(...)
        +ApplyState(...)
        +SimulateTick(...)
    }
    class MultiplayerPlayerAvatar {
        <<NetworkBehaviour>>
        +OnNetworkSpawn()
        +SubmitOwnerInputServerRpc(...)
        +SubmitOwnerActionIntentServerRpc(...)
        +PushAuthoritativeLocomotionStateClientRpc(...)
        +PushApprovedActionStartClientRpc(...)
        +PushReactionSnapshotClientRpc(...)
        +PushAttackHitFeedbackClientRpc(...)
        +SubmitRetryReadyIfOwner()
        +TryGetReplicatedHealth(out int, out int) bool
        +IsReplicatedDead bool
        +IsRetryReady bool
        +GetActiveAvatarCount() int
    }
    class MultiplayerBossAuthorityBridge {
        <<MonoBehaviour>>
        +Update()
        +HandleNetworkTick()
        +TryGetLatestBossState(out BossAuthoritativeState) bool
        +HasLatestBossState bool
        +IsBossDead bool
    }
    class HostPlayerActionValidator { +TryValidate(...) }
    class HostPlayerReactionResolver {
        +SeedFromRuntime(...)
        +SyncRuntimeState(...)
        +TryResolveBossHit(...)
        +TryRecordDamageContribution(...)
    }
    class MultiplayerPlayerPresentationDriver {
        +RefreshBindings()
        +HandleSimulationModeChanged(...)
        +UpdatePredictedLocomotionPresentation(PlayerInputPacket)
        +GetPreferredCameraFollowPosition() Vector3
    }
    class MultiplayerLocalPlayerRegistry {
        <<Static>>
        +LocalPlayer PlayerController
        +SetLocalPlayer(PlayerController)
        +Clear()
    }
    class PlayerVisual { +Animator Animator }
    class BlinkWhiteEffect {
        +PlayBlink(float)
        +PlaySingleBlink()
        +SetBlink(bool)
        +StopBlink()
    }
    class ThirdPersonCameraController {
        <<MonoBehaviour>>
        +LateUpdate()
        +FollowOffset Vector3
        +MouseYawPitchControl
    }
    class CombatHUDController {
        +Initialize(Health, Health)
        +SetPlayerHpNormalized(float, int, int)
        +SetPlayerName(string)
        +SetBossName(string)
        +SetPartnerName(string)
        +SetPartnerHpNormalized(float, int, int)
        +SetPartnerHudVisible(bool)
        +SetDashReadyNormalized(float)
        +SetPlayerTorso(Sprite)
        +ShowCombo(int)
        +HideCombo()
        +ShowHud(bool)
        +ShowDamageFeedback(bool, int)
    }
    class DamageCaster {
        +OnAttackHitConfirmed Action
        +OnAttackWindowResolved Action~bool,int~
        +EnableHitbox(int)
        +DisableHitbox()
    }
    class Health {
        +CurrentHP int
        +OnDamageTaken Action
        +OnDeath Action
    }

    %% Concrete States
    class MoveState { +Update() }
    class DashState { +Update() }
    class JumpState { +Update() }
    class AttackState { +Update() }
    class HitState { +Update() }
    class StunState { +Update() }
    class DeadState { +Update() }

    %% Relationships
    LocalInputProvider ..|> IInputProvider : implements
    MultiplayerBufferedInputProvider ..|> IInputProvider : implements
    PlayerController ..|> IDashContext : implements
    PlayerController ..|> IAttackable : implements
    PlayerController ..|> IBossAttackHitReceiver : implements
    Health ..|> IDamageable : implements

    PlayerController --> IInputProvider : uses
    PlayerController --> PlayerVisual : controls
    PlayerController --> BlinkWhiteEffect : triggers (post-stun blink)
    PlayerController --> DamageCaster : controls/subscribes
    PlayerController --> CombatHUDController : updates
    PlayerController --> Health : owns
    PlayerController --> PlayerLocomotionCore : delegates shared locomotion sim
    PlayerController --> MultiplayerPlayerPresentationDriver : delegates multiplayer presentation
    ThirdPersonCameraController --> PlayerController : reads look cache / injects CameraRoot
    ThirdPersonCameraController --> MultiplayerLocalPlayerRegistry : resolves local owner in multiplayer
    MultiplayerPlayerAvatar --> PlayerController : configures runtime role / starts approved action / applies owner reaction
    MultiplayerPlayerAvatar --> LocalInputProvider : toggles local owner input
    MultiplayerPlayerAvatar --> MultiplayerBufferedInputProvider : writes remote owner input
    MultiplayerPlayerAvatar --> ClientToHostPlayerActionIntent : emits action edges
    MultiplayerPlayerAvatar --> HostPlayerActionValidator : validates dash/attack start
    MultiplayerPlayerAvatar --> HostPlayerReactionResolver : resolves host reaction
    MultiplayerPlayerAvatar o-- HostPlayerState : keeps host truth
    MultiplayerPlayerAvatar o-- HostToClientPlayerReactionSnapshot : caches latest reaction
    MultiplayerPlayerAvatar o-- MultiplayerLocomotionState : snapshots/replay
    MultiplayerBossAuthorityBridge --> BossAuthoritativeState : sends/applies dedicated boss state
    ClientToHostPlayerActionIntent --> InputFlag : reuses action bits
    HostPlayerActionValidator --> HostPlayerState : writes accepted action
    HostPlayerReactionResolver --> HostPlayerState : updates
    HostPlayerReactionResolver --> HostToClientPlayerReactionSnapshot : creates

    PlayerBaseState --|> BaseState : extends
    MoveState --|> PlayerBaseState : extends
    DashState --|> PlayerBaseState : extends
    JumpState --|> PlayerBaseState : extends
    AttackState --|> PlayerBaseState : extends
    HitState --|> PlayerBaseState : extends
    StunState --|> PlayerBaseState : extends
    DeadState --|> PlayerBaseState : extends

    DamageCaster ..> IDamageable : depends
    DamageCaster ..> IBossAttackHitReceiver : depends (Boss attack metadata)
    LocalInputProvider o-- PlayerInputPacket : creates
```

### 2.2. Boss AI Architecture (The Dragon)
거리 기반 상태 전환, closest-target base + recent-damage aggro override, 비주얼 분리(BossVisual)가 적용된 보스 전용 구조입니다.

**관련 코드:**
*   **Controller**: `Assets/Scripts/Boss/BossController.cs`
*   **Visual**: `Assets/Scripts/Boss/BossVisual.cs`
*   **States**: `Assets/Scripts/Boss/BossFSM.cs` (모든 Boss State 클래스 포함)
*   **Attack Patterns**: `Assets/Scripts/Boss/Attacks/` (`IBossAttackPattern.cs`, `BasicAttackPattern.cs`, `LungeAttackPattern.cs`, `ProjectileAttackPattern.cs`, `AoEAttackPattern.cs`)
*   **Combat**: `Assets/Scripts/Common/Combat/Health.cs`, `Assets/Scripts/Boss/AttackWarningController.cs`, `Assets/Scripts/Common/Combat/BossHitBox.cs`

```mermaid
classDiagram
    direction TB

    %% Components
    class BossController {
        <<MonoBehaviour>>
        +MoveSpeed float
        +DetectionRange float
        +AggroPriorityRange float
        +AggroTime float
        +BasicAttackRangeOrigin Transform
        +BasicAttackRange float
        +LungeAttackRange float
        +SharedRangedAttackRange float
        +ChaseReengageBuffer float
        +CanAttack bool
        +IsLocomotionVisualSuppressed bool
        -StateMachine~BossBaseState~ _stateMachine
        +BossVisual Visual
        +BasicAttackSettings BasicAttackSettings
        +LungeAttackSettings LungeAttackSettings
        +ProjectileAttackSettings ProjectileAttackSettings
        +BossProjectilePool ProjectilePool
        +Transform ProjectileSpawnPoint
        +SetLocomotionVisualSuppressed(bool)
        +GetPlanarDistanceToTarget() float
        +GetPlanarDistanceFromBasicAttackOriginToTarget() float
        +IsTargetInsideBasicAttackArc() bool
        +IsTargetInDetectionRange() bool
        +RegisterAggroContribution(GameObject, int) void
        +RefreshClosestLiveTarget(bool) void
        +GetPlanarDistance(Vector3, Vector3) float
        +CaptureAuthoritativeState(int, float) BossAuthoritativeState
        +BeginAuthoritativeAttack(IBossAttackPattern)
        +EndAuthoritativeAttack()
        +ShowBasicAttackTelegraph(float)
        +HideBasicAttackTelegraph()
        +TryEnterBasicAttackTelegraphActiveNow(string) bool
        +ShowLungeAttackTelegraph(float, float, int)
        +HideLungeAttackTelegraph()
        +Update()
        +MoveRaw(Vector3, float)
        +RotateTowardsImmediate(Vector3)
        +BeginLungeTravelDirectionLock(Vector3)
        +EndLungeTravelDirectionLock()
        +ApplyLungeRootMotion(Vector3)
    }

    class BossBaseState {
        <<Abstract>>
        +Update()*
    }

    class BossVisual {
        <<MonoBehaviour>>
        +Animator Animator
        +SetSpeed(float)
        +PlayIdle()
        +PlayMove()
        +PlayAttack()
        +PlayLungeAttack()
        +SetLungeRootMotionEnabled(bool)
        +PlayProjectileAttack()
        +PlayTakeOff()
        +PlayFlyForward()
        +PlayFlyIdle()
        +PlayLand()
        +TriggerHit()
        +TriggerDie()
        +SetSearchingUI(bool)
    }
    class BlinkWhiteEffect { +PlaySingleBlink() +StopBlink() }

    class BossHitBox {
        <<MonoBehaviour>>
        +Owner Health
        +TakeDamage(int)
        +ReportDamageContribution(GameObject, int)
    }

    class AttackWarningController {
        +StartWarningSector(...)
        +StartDamageSector(...)
        +StartDamageStrip(...)
        +ForceEnd()
    }
    class Health { +CurrentHP int +OnDamageTaken Action~int~ +OnDeath Action }
    class BossAuthoritativeState { <<Struct>> }
    class BossAuthoritativeLocomotionState { <<Enumeration>> }
    class BossAuthoritativeAttackId { <<Enumeration>> }
    class BossAuthoritativeAttackVisualState { <<Enumeration>> }
    class BossAuthoritativePhase { <<Enumeration>> }
    class MultiplayerBossAuthorityBridge {
        <<MonoBehaviour>>
        +Update()
        +HandleNetworkTick()
    }

    %% Concrete States
    class BossIdleState { +Update() }
    class BossCombatState { +Update() }
    class BossAttackState { +Update() }
    class BossSearchingState { +Update() }
    class BossHitState { +Update() }
    class BossDeadState { +Update() }

    %% Relationships
    BossController --> BossVisual : Controls
    BossController --> BlinkWhiteEffect : triggers (damage blink)
    BossController --> Health : Uses
    BossController --> AttackWarningController : Controls/reuses
    BossController --> BossAuthoritativeState : captures host truth
    BossHitBox --> BossController : forwards dealer contribution
    MultiplayerBossAuthorityBridge --> BossController : captures host truth / applies display-only state
    MultiplayerBossAuthorityBridge --> BossVisual : drives client display-only animation
    MultiplayerBossAuthorityBridge --> CombatHUDController : writes boss HUD from authoritative HP

    BossBaseState --|> BaseState
    BossBaseState <|-- BossIdleState
    BossBaseState <|-- BossCombatState
    BossBaseState <|-- BossAttackState
    BossBaseState <|-- BossSearchingState
    BossBaseState <|-- BossHitState
    BossBaseState <|-- BossDeadState

    IDamageable <|.. BossHitBox : Implements
    BossHitBox --> Health : Delegates
    AttackWarningController ..> IDamageable : Hits
```

**현재 배치 원칙:**
* Attack1/Attack2 판정과 경고 표시는 `AttackWarningController`를 source-of-truth로 사용한다.
* `HeadDamageCaster`, `LungeDamageCaster`는 legacy reference만 유지하고 current basic/lunge damage truth에는 사용하지 않는다.
* Attack1 bite hit는 sampled end-pose + front arc sector를 사용하고, Attack2는 fixed travel distance + strip path를 사용한다.
* Attack1 tuned warning hide는 `readyDuration` 기반 warning duration이 끝나기 전에는 full end를 허용하지 않아, `EnterActivePhase()` 이전 `ForceEnd()`로 damage truth가 사라지지 않게 보호한다.
* `Head`, `Body`의 `BossHitBox`와 Collider는 피격용이므로 Visual/Bone 계층에 유지한다.

### 2.3. Boss Attack System (Strategy Pattern)
공격 패턴의 확장성을 위해 `Strategy Pattern`을 적용했습니다. `BossAttackState`는 구체적인 공격 로직을 알지 못하며, 주입된 `IBossAttackPattern`에게 실행을 위임합니다.

**관련 코드:**
*   **Attack Patterns**: `Assets/Scripts/Boss/Attacks/` (`IBossAttackPattern.cs`, `BasicAttackPattern.cs`, `LungeAttackPattern.cs`, `ProjectileAttackPattern.cs`, `AoEAttackPattern.cs`)
*   **Projectile Pooling**: `Assets/Scripts/Boss/Projectiles/` (`BossProjectilePool.cs`, `BossProjectile.cs`)

```mermaid
classDiagram
    direction TB

    class BossController {
        +BasicAttackPattern BasicAttackPattern
        +LungeAttackPattern LungeAttackPattern
        +ProjectileAttackPattern ProjectileAttackPattern
        +AoEAttackPattern AoEAttackPattern
        +BossProjectilePool ProjectilePool
        +StartAttackCooldown()
        +AttackDamage int
    }

    class BossAttackState {
        -IBossAttackPattern _currentPattern
        +SetPattern(IBossAttackPattern)
        +Enter()
        +Update()
        +Exit()
    }

    class IBossAttackPattern {
        <<Interface>>
        +Enter(BossController)*
        +Update(BossController)* bool
        +Exit(BossController)*
    }

    class BasicAttackPattern {
        +Enter(BossController)
        +Update(BossController) bool
        +Exit(BossController)
    }

    class BasicAttackSettings {
        +readyDuration float
        +readyNormalizedWindow Vector2
        +hitHalfAngle float
        +telegraphHideNormalizedTime float
    }

    class LungeAttackPattern {
        -LungeAttackSettings _settings
        +Enter(BossController)
        +Update(BossController) bool
        +Exit(BossController)
    }

    class LungeAttackSettings {
        +damageMultiplier float
        +damageCastNormalizedWindow Vector2
        +travelDistance float
        +pathWidth float
    }

    class ProjectileAttackPattern {
        -ProjectileAttackSettings _settings
        -float _warningTimer
        -float _volleyTimer
        -float _postFireRecoveryTimer
        -int _shotsFired
        +Enter(BossController)
        +Update(BossController) bool
        +Exit(BossController)
    }

    class AoEAttackPattern {
        -PatternPhase _phase
        -float _phaseTimer
        -float _spawnTimer
        +Enter(BossController)
        +Update(BossController) bool
        +Exit(BossController)
    }

    class BossProjectile {
        +Initialize()
        +Update()
        +OnTriggerEnter(Collider)
        +OnCollisionEnter(Collision)
    }

    class BossProjectilePool {
        +TryGetProjectile() BossProjectile
        +ReturnProjectile(BossProjectile)
    }

    %% Relationships
    BossAttackState --> IBossAttackPattern : Delegates (Context -> Strategy)
    IBossAttackPattern <|.. BasicAttackPattern : Implements
    IBossAttackPattern <|.. LungeAttackPattern : Implements
    IBossAttackPattern <|.. ProjectileAttackPattern : Implements
    IBossAttackPattern <|.. AoEAttackPattern : Implements
    BossController --> BasicAttackPattern : Owns
    BossController --> BasicAttackSettings : Owns
    BasicAttackPattern --> BasicAttackSettings : Reads
    BossController --> LungeAttackPattern : Owns
    LungeAttackPattern --> LungeAttackSettings : Uses
    BossController --> ProjectileAttackPattern : Owns
    BossController --> AoEAttackPattern : Owns
    BossController --> BossProjectilePool : Owns
    ProjectileAttackPattern --> BossProjectilePool : Uses
    AoEAttackPattern --> BossProjectilePool : Uses
    BossProjectilePool --> BossProjectile : Reuses
```

### 2.4. Game Flow Architecture (Title -> Loading -> GamePlay)
타이틀 입력, 로딩 연출, 전투 씬 진입을 분리한 게임 루프 시작 구간 구조입니다.

**관련 코드:**
*   **Flow Entry**: `Assets/Scripts/Common/TitleSceneController.cs`
*   **Transition Router**: `Assets/Scripts/Common/SceneLoader.cs`
*   **Loading Orchestrator**: `Assets/Scripts/Common/LoadingSceneController.cs`
*   **Result & Restart**: `Assets/Scripts/Common/GameManager.cs`

```mermaid
classDiagram
    direction TB

    class TitleSceneController {
        <<MonoBehaviour>>
        -GameSceneId _nextSceneId
        -float _inputLockDuration
        -float _hostStartEnableDelay
        -bool _keepRuntimeRootInEditMode
        -TitlePanelState _currentPanelState
        -LobbyRole _currentLobbyRole
        +OnEnable()
        +Awake()
        +Update()
        +ShowPanel()
        +HandleSoloPlaySelected()
        +ShowMultiplayerLobby(...)
    }

    class SceneLoader {
        <<Static>>
        +Load(GameSceneId)
        +TryConsumeTargetScene(out string) bool
        +NotifyTransitionCompleted()
        +CancelPendingTransition()
    }

    class LoadingSceneController {
        <<MonoBehaviour>>
        -GameSceneId _fallbackScene
        +BeginLoading()
        +Update()
    }

    class GameManager {
        <<MonoBehaviour>>
        +ResolveGameOver(GameResult)
        +ResolveMultiplayerGameOver()
        +RestartCurrentScene()
    }

    %% Relationships
    TitleSceneController ..> SceneLoader : requests transition
    SceneLoader --> LoadingSceneController : reserves target
    LoadingSceneController ..> SceneLoader : consumes target/completes transition
    GameManager ..> SceneLoader : shares game flow context
```

`TitleSceneController`는 `MultiplayerModePanel / HostCreatePanel / ClientJoinPanel / LobbyPanel / WrongKeyPopup`을 title flow 검증용 패널 묶음으로 유지한다.
이 경로는 title flow/layout 확인과 menu state transition 확인에 사용한다.
아트 통합 과정에서 source snapshot scene이 필요할 때는 `Assets/Scenes/merged/TitleScene.unity`를 보관 경로로 사용하고, runtime main path는 `Assets/Scenes/mutiplayer/TitleScene.unity`를 유지한다.
runtime title UX는 `Press Any Key` gate를 먼저 통과한 뒤 `TitleMainPanel`을 여는 순서를 사용하며, gate 입력은 keyboard/mouse만 허용하고 gamepad/joystick button은 제외한다. `TitleSceneController`의 runtime root 바인딩은 `TitleRuntimeRoot (1) -> TitleRuntimeRoot` 우선순위를 사용한다. 선택된 runtime root는 Play Mode에서 center anchor + local zero 기준으로 고정되고, recovered Animator는 runtime layout override를 막기 위해 비활성화한다. `TitleMainPanel`의 위치는 runtime에서 강제 재정렬하지 않고 scene에 배치된 `RectTransform` 값을 유지한다. `Text_PressAnyKey` layout은 scene `RectTransform`을 source-of-truth로 유지하고, legacy recovered Animator는 비활성 상태를 기본 계약으로 둔다. same rule은 `TitleCanvas/Background/Image`에도 적용하며, `Background/Image` 오브젝트는 활성 상태를 유지하고 recovered Animator만 비활성으로 고정해 배경 image와 하위 장식 오브젝트의 상대 배치를 고정한다.

---

## 3. Current Implementation Snapshot

아래 표는 일자별 작업 기록이 아니라, 현재 런타임 계약을 빠르게 확인하기 위한 구조 스냅샷입니다.
세부 규칙, 실험 기록, 검증 로그는 각 전용 문서를 기준으로 유지합니다.

### 3.1. Core Systems & Input
| Component | Note |
| --- | --- |
| **Input Provider Layer** | `IInputProvider`를 기준으로 local input과 multiplayer buffered input을 분리한다. |
| **Input Packet** | `PlayerInputPacket`은 bit-packed 버튼 입력과 직렬화 가능한 데이터만 운반한다. |
| **StateMachine** | Player와 Boss는 `StateMachine` 기반으로 상태를 전환하고, Controller는 실행 진입점만 노출한다. |
| **Physics & Pooling** | 물리 판정은 NonAlloc 경로를 기준으로 하며, 보스 투사체는 `BossProjectilePool`로 재사용한다. |
| **Camera Module** | `ThirdPersonCameraController`가 `CameraRoot`와 look 입력 기반 orbit을 담당한다. 2026-04-07 spectator follow-up 기준 multiplayer에서 local avatar가 dead이고 partner가 alive면, local death edge 뒤 약 `2.5초`를 기다린 다음 local look/orbit ownership은 유지한 채 follow position만 alive partner 쪽으로 전환한다. |
| **Multiplayer Runtime Bridge** | `MultiplayerPlayerAvatar`, `MultiplayerBossAuthorityBridge`, `PlayerLocomotionCore`, `MultiplayerPlayerPresentationDriver`가 각각 player authority glue, boss authority bridge, shared locomotion, local presentation을 분담한다. 2026-04-07 result flow follow-up 기준 avatar는 replicated HP/dead뿐 아니라 retry-ready bit와 active avatar registry도 같이 들고, `GameManager`는 이 shared multiplayer bridge surface를 result/retry count source로 재사용한다. same-day cleanup 이후 temporary action trace, disconnect/session continuity debug, boss lunge debug spam은 제거하고 current verify에는 warnings/errors 위주로 남긴다. |
| **Package Baseline** | integration branch는 multiplayer package set(`com.unity.netcode.gameobjects`, `com.unity.transport`, `com.unity.services.authentication`, `com.unity.services.lobby`, `com.unity.services.relay`)과 art-side add-on(`com.unity.2d.sprite`, `com.unity.formats.fbx`, `com.unity.toonshader`)을 함께 유지해야 한다. 한쪽만 남기면 `docs/temp_console_log.txt`와 같은 missing namespace compile failure가 재발한다. |
| **Art Scene Recovery** | current integration branch에서 legacy art/title/loading scene은 merge 중 old dependency를 잃을 수 있다. current recovery baseline은 `downloaded_art_things/Assets`에서 GUID가 일치하는 source pack/folder(`CombatGirlsCharacterPack`, `Hits Effects FREE`, `UNI VFX`, `InfographicElements_UI`, legacy background/character/lunar mesh)를 먼저 복구하고, source가 끝까지 없는 missing script/material GUID만 `Assets/Scripts/Test/MissingArtSceneScript_*.cs`, `Assets/Scripts/Test/MissingLegacySceneScript_*.cs`, `Assets/RecoveredArt/Materials/MissingLegacySceneMaterial_*.mat` placeholder로 임시 연결해 scene deserialize를 다시 통과시키는 방식이다. latest asset repair follow-up에서는 same downloaded source를 사용해 current `Assets/` 안 Git LFS pointer text로 남아 있던 art file 276개를 real binary로 다시 덮어썼고, duplicated `CombatGirlsCharacterPack` runtime/editor script copy는 current import가 실제로 쓰지 않는 root `Assets/CombatGirlsCharacterPack/Scripts`와 `Assets/CombatGirlsCharacterPack/Biperworks_Tools/Editor`만 제거해 compile collision을 멈췄다. same reimport follow-up에서는 missing nested prefab source `Assets/PixPlays/ElementalProjectiles/Windbullet/Version_BuiltIn/WindbulletHit/WindbulletHit.prefab`도 expected guid/fileID를 맞춘 placeholder로 다시 연결했다. 2026-04-17 terrain checkerboard hotfix에서는 `Assets/New Terrain.asset`이 요구하는 7개 external TerrainLayer GUID에 맞춘 bridge asset(`Assets/RecoveredArt/TerrainLayers/LegacyTerrainLayer_01~07.terrainlayer`)을 추가해 GUID resolve를 복구했다. current verify 기준 built-in/scene GUID를 제외한 `Assets/Scenes/**/*.unity` effective missing GUID count는 `0`, current `Assets/` pointer count도 `0`이다. |
| **Balance Tooling** | `Assets/Editor/PlayerBossBalanceToolWindow.cs`는 `Tools/Balance/Open Player Boss Balance Tool` editor window를 제공한다. one combined JSON file 안에 `player` / `boss` section을 묶고, current scope에서는 `Health` max HP와 `PlayerController`/`BossController`의 selected balance field를 export/import 한다. boss section에는 `basicAttackRange`도 포함되며, older JSON에 이 field가 없으면 current controller value를 유지하는 backward-safe import guard를 둔다. target은 prefab asset과 verify scene 둘 다 지원하며, scene path는 `Assets/Scenes/mutiplayer/GamePlayScene_Verify.unity`, default player prefab path는 `Assets/Resources/Prefabs/MultiplayerPlayerAvatar.prefab`를 기준으로 한다. |
| **Branch Asset Export Workflow** | `tools/asset_export/export_roots.json`, `tools/asset_export/Export-BranchAssetArtifacts.ps1`, `Assets/Editor/BranchAssetExportRunner.cs`가 current branch asset export contract를 담당한다. root rule은 gradual `Assets/Project/Logic` + `Assets/Project/Content` split을 기준으로 하고, export source는 raw `git diff`가 아니라 repo-tracked allowlist를 사용한다. output contract는 branch slug 기준 `one .unitypackage + one raw zip + one manifest`이며, file name은 `<branch_slug>_delta_unity_assets_<YYYYMMDD>`, `<branch_slug>_raw_downloads_<YYYYMMDD>`, `<branch_slug>_manifest_<YYYYMMDD>`를 따른다. code/docs는 Git에 남기고, placeholder recovery script(`MissingLegacySceneScript_*`, `MissingArtSceneScript_*`)는 default exclude로 유지한다. `Assets/Resources/**`는 generic content bucket이 아니라 runtime-special exception이며, current prefab resource root는 `Assets/Resources/Prefabs`를 기준으로 관리한다. current batch export는 `-noUpm` 없이 normal package graph를 사용하고, `Temp/UnityLockfile`, `Library/SourceAssetDB-lock`, `Library/ArtifactDB-lock` preflight를 먼저 확인해 project-open 상태면 Unity launch 전에 clear message로 중단한다. automation shell에서는 `HOME`, `LOCALAPPDATA`, `APPDATA`, `PROGRAMDATA`, `ALLUSERSPROFILE` fallback을 먼저 보강하고, `UnityPackageManager.exe`를 IPC mode로 prestart한 뒤 Unity batch launch에 `-upmIpcPath`를 전달해 UPM bootstrap timeout을 피한다. raw archive 단계는 `System.IO.Compression` 기반 zip writer로 same output folder 안에서 함께 생성한다. |

### 3.2. Player System
| Component | Note |
| --- | --- |
| **Movement Logic** | 이동/대시/점프 로직은 각 Player State가 판단하고, 실제 이동 실행은 `PlayerController`가 담당한다. |
| **Attack Logic** | `AttackState`는 콤보, 캔슬, 개별 데미지 윈도우를 처리하며, multiplayer에서는 Host-approved action start를 기준으로 동작한다. `SwordSlashSpawner` 같은 weapon-side visual helper도 velocity 조건 단독이 아니라 `AttackState` 게이트를 먼저 통과한 뒤에만 slash VFX를 스폰해야 한다. |
| **Camera & Presentation** | 카메라 입력과 `CameraRoot` 관리는 `ThirdPersonCameraController`가 맡고, multiplayer local 화면 보정은 `MultiplayerPlayerPresentationDriver`가 visual-only helper로 담당한다. dead local player spectator는 alive partner의 exact camera를 공유하지 않고, dead player의 own orbit input을 유지한 채 약 `2.5초` 뒤 partner `GetPreferredCameraFollowPosition()`만 따라간다. |
| **Hit & Reaction** | 플레이어는 `IBossAttackHitReceiver`를 통해 보스 공격 메타데이터를 받고, hit/stun/death 반응은 authoritative snapshot apply를 기준으로 정리한다. |
| **HUD Binding** | `CombatHUDController`는 player/boss HP, combo, damage feedback을 관리한다. multiplayer에서는 player HUD는 avatar-driven authoritative path를 쓰고, boss HUD는 local boss `Health` 대신 `MultiplayerBossAuthorityBridge`가 boss authoritative snapshot으로 직접 갱신한다. combo/damage feedback은 local hitbox callback이 없는 `ClientOwnerProxy`에서도 `PushAttackHitFeedbackClientRpc(totalDamage, comboStep)` -> `PlayerController.ApplyAuthoritativeAttackHudFeedback(...)` 경로로 same HUD에 직접 반영한다. 여기서 `comboStep`는 Host가 hit resolve 시점의 mutable combo state를 다시 읽지 않고, `PlayerController.OnHitStart()`가 캡처한 attack-window step을 그대로 사용한다. fixed damage feedback text는 serialized TMP alpha를 visibility cap으로 존중하며, `Text_DamageFeedback` alpha가 `0`이면 runtime path가 alpha `1`을 강제로 복구하지 않는다. dash icon fill은 `PlayerController.UpdateDashHudPresentation()`이 `CombatHUDController.SetDashReadyNormalized(...)`로 매 프레임 반영하며, `fill = 1 - (DashCooldownRemaining / DashCooldown)` 공식을 기본으로 사용하고 dash ready일 때 `1`로 고정한다(press 직후에는 cooldown 시작값 때문에 자연스럽게 `0`에서 시작). same viewer-side HUD path는 portrait에도 적용되어, Host 화면은 Host portrait가 좌측 main HUD에, Client 화면은 Client portrait가 좌측 main HUD에 오도록 `MultiplayerPlayerAvatar`가 local viewer 기준으로 portrait layout도 다시 쓴다. `MultiplayerPlayerAvatar.TryGetReplicatedHealth(...)`도 same HUD replica를 result-flow read source로 재사용한다. |
| **Animator & Validation** | `PlayerAnimatorGuard`가 플레이어 Animator 상태/이벤트 계약의 누락을 점검하고 복구 경로를 제공한다. 2026-04-08 player locomotion follow-up 기준 `PlayerController.SetLocomotionAnimatorSpeed(...)`가 solo `MoveState`, shared `PlayerLocomotionCore`, multiplayer `MultiplayerPlayerAvatar`의 `Speed` write path를 한곳으로 모으고, default `0.08s` damping으로 quick opposite turn neutral frame의 idle cut-in을 완화한다. same-day debug follow-up에서는 `PlayerController.enableMovementDebugLog`가 owner client의 root / authoritative correction / render proxy trace를 열고, `MultiplayerPlayerAvatar`와 `MultiplayerPlayerPresentationDriver`가 `ClientPredict/AuthBaseline/AuthSkip/AuthReplay/Proxy` 로그를 찍는다. same-day client jitter follow-up에서는 `PredictedLocomotion + ClientOwnerProxy + local presentation enabled + MoveState` 조합의 owner locomotion `Speed`를 network tick/replay path가 더 이상 supply하지 않고, `PlayerController` normal `Update`가 current input + latest predicted planar speed cache를 읽어 single writer로 적용한다. stop window polish로는 brief neutral frame은 damping을 유지하되, input과 predicted planar speed가 함께 `0`인 상태가 `0.03s`를 넘기면 immediate `0` settle을 걸어 lingering walk blend를 정리한다. 2026-04-17 visual swap follow-up에서는 `PlayerController`가 serialized `playerVisual`/`blinkWhiteEffect`를 validate하고, stale scene wiring일 때 active child `PlayerVisual` 기준으로 local presentation binding을 다시 resolve한다. same-day blink follow-up에서는 `BlinkWhiteEffect`가 runtime blink material 생성 시 source material의 `_BaseMap/_MainTex`, `_BaseColor/_Color`를 blink shader 입력으로 다시 복사해 toon material fallback을 유지하고, replacement avatar는 `targetRenderers`에 body `SkinnedMeshRenderer`를 직접 지정해 renderer scope를 명시한다. multiple valid child visual이 남아 있으면 warning으로 scene cleanup 필요를 surface한다. latest inspector follow-up에서는 `PlayerController`가 `_strictVisualBinding`/`_visualBindingReport`를 제공해 current visual/blink reference path와 invalid reason을 inspector에서 바로 확인할 수 있고, context menu `Validate Visual Bindings`로 manual validation을 실행할 수 있다. dash clip transition은 existing network sim path에 남기고, gameplay move/rotate/correction truth는 바꾸지 않는다. |

### 3.3. Boss System (The Dragon)
| Component | Note |
| --- | --- |
| **Boss Logic (FSM)** | `BossController`는 Idle, Combat, Attack, Searching, Hit, Dead 흐름을 상태 머신으로 관리한다. |
| **Boss Sensors** | 타겟 감지와 공격 거리 평가는 Y를 제외한 XZ 거리 기준으로 수행한다. 기본 타겟 획득 fallback은 closest live player지만, current target이 `AggroPriorityRange` 안에 있는 동안에는 attack 전/후에도 그 target을 계속 유지한다. boss가 어느 플레이어에게서든 첫 피격을 받으면 `AggroTime`이 시작되고, 타이머 동안 current target을 유지한 채 cycle damage를 누적한다. 타이머가 끝난 뒤 두 플레이어가 `AggroPriorityRange` 안에 함께 있으면 current cycle damage winner로 target을 한 번 교체하고, 새 피격이 들어오면 다음 cycle이 다시 시작된다. current implementation의 target refresh path는 `scan live targets -> resolve aggro priority -> apply target if changed` 순서로 분리해 유지한다. inspector tuning은 detection-related 값(`AggroPriorityRange`, `DetectionRange`, `BasicAttackRange`, `LungeAttackRange`, `SharedRangedAttackRange`, `ChaseReengageBuffer`, `SearchDuration`)을 `Detection Settings`에 모으고, `AggroTime`만 `Aggro Settings`에 분리한다. |
| **Boss Navigation** | 추적 이동, 즉시 회전, attack-range hysteresis, locomotion visual suppression을 분리해 관리한다. aggro timer는 `AttackState`와 phase intro 중에는 pause되어 animation 전환을 방해하지 않는다. |
| **Boss Combat** | 공격 선택은 pattern range filtering과 `Strategy Pattern`을 기준으로 구성되며, Basic/Lunge/Projectile/AoE 슬롯을 같은 attack state에서 위임한다. Phase1 Basic entry gate는 `BossController.BasicAttackRange`와 `BossController.IsTargetInsideBasicAttackArc()`를 함께 읽어 target이 sampled bite end-pose 기준 mouth front hemisphere 안에 있을 때만 고른다. Basic/Lunge actual damage와 warning visual은 current build에서 `AttackWarningController`가 담당한다. Phase2의 Projectile/AoE entry gate는 `SharedRangedAttackRange` 하나를 공용 source로 사용한다. |
| **Attack Warning Ownership** | Attack1/Attack2는 body/head `DamageCaster` 대신 `AttackWarningController`를 source-of-truth로 사용한다. Attack1은 sector warning + one-shot sector damage, Attack2는 strip warning + fixed-distance strip damage를 사용한다. Attack1 tuned hide는 warning duration이 실제로 끝난 뒤에만 full end를 허용해 pre-active `ForceEnd()` regression을 막는다. pre-active hide 요청은 `DEFER_PREACTIVE`로 처리해 active phase 진입 전 강제 종료를 막고, lunge close는 one-shot guard로 동일 hide 호출 스팸을 차단한다. Basic은 `readyNormalizedWindow.y` crossing frame에서 `TryEnterBasicAttackTelegraphActiveNow(...)`를 직접 호출해 Attack1 damage open을 ready normalized 경계에 맞춘다. legacy `HeadDamageCaster` / `LungeDamageCaster` reference는 old anchor fallback 용도로만 남긴다. |
| **Lunge Contract** | Lunge는 fixed `travelDistance`와 `pathWidth`를 source-of-truth로 사용하고, target distance가 아니라 start facing direction만 잠근다. warning strip은 display rule에 따라 early hide될 수 있지만, actual boss travel은 `damageCastNormalizedWindow` 전체 구간을 기준으로 유지한다. |
| **Boss Authority Contract** | current `step 1-2` 기준 `BossController`는 `CaptureAuthoritativeState(...)`로 dedicated boss snapshot을 만들고, `MultiplayerBossAuthorityBridge`가 이 snapshot을 Host runtime-root에서 capture/send 한다. snapshot은 transform / locomotion state / current attack id / current attack visual state / attack start server tick / current attack `normalized time` / current attack playback speed / HP / phase / dead flag를 담는다. `AttackStartServerTick`는 fallback attack clock으로 유지하고, basic ready-slice처럼 host animator speed override가 있는 경우에는 actual animator progress가 추가 source-of-truth가 된다. AoE airborne replay follow-up에서는 same snapshot의 `AttackVisualState`가 `TakeOff / FlyForward / FlyIdle / Land` semantic phase를 client에 직접 전달한다. |
| **Boss Multiplayer Read** | multiplayer client는 boss gameplay truth를 직접 계산하지 않고, disabled local boss object에 `MultiplayerBossAuthorityBridge`가 latest dedicated state의 display-only transform/semantic animator state를 apply하는 구조를 기준으로 한다. current baseline은 direct apply이며, same received boss tick은 한 번만 consume하고, move/search locomotion speed는 weak packet delta에서도 host locomotion speed를 바닥값으로 유지한다. basic attack은 `AttackNormalizedTime / AttackPlaybackSpeed` snapshot을 매 consume마다 다시 적용해 ready-slice drift를 줄이고, AoE는 `AttackVisualState` 변화를 기준으로 `TakeOff -> FlyForward -> FlyIdle -> Land` 비행 phase를 순서대로 재생한다. boss HUD도 같은 bridge가 `CurrentHealth/MaxHealth` snapshot을 local `CombatHUDController`에 직접 써서 local disabled boss `Health`와 분리한다. same bridge는 `TryGetLatestBossState(...)` / `IsBossDead` read surface로 later result flow의 boss truth source도 제공한다. client gameplay prediction/extrapolation은 넣지 않는다. |
| **Boss Effect Replay** | attack 1/2 warning과 attack 3/4의 spawned projectile/AoE visual은 `BossReplicatedEffectEvent`로 별도 전송한다. Host는 basic sector / lunge strip warning의 `show/hide`, projectile shot, AoE spawn을 same reliable named message batch에 큐잉하고, `MultiplayerBossAuthorityBridge`가 remote client에 보낸다. client는 local `AttackWarningController`, pooled `BossProjectile`, `AoECircleController`를 display-only로 재생한다. damage truth는 계속 Host-only다. |

### 3.4. User Interface (UI)
| Component | Note |
| --- | --- |
| **Combat HUD** | `CombatHUDController`는 HP bar, combo UI, damage feedback, dash cooldown fill, 전체 HUD visibility를 제어한다. multiplayer boss HP는 `MultiplayerBossAuthorityBridge`가 authoritative snapshot 기준으로 직접 반영한다. fixed damage feedback은 `Text_DamageFeedback` inspector alpha를 visibility cap으로 사용해, scene/prefab에서 alpha를 `0`으로 내리면 runtime에서도 hidden intent를 유지한다. same HUD controller는 prefab 안의 기본 Host/Client portrait pair를 캐시하고, local viewer가 누구인지에 따라 좌측 main portrait와 partner portrait를 swap한다. |
| **Player/Boss Labels** | HUD는 player, boss, partner label과 HP source를 분리해 solo/multiplayer 양쪽에 대응한다. multiplayer에서는 label뿐 아니라 portrait도 viewer-relative rule을 따라, Host 화면은 `Host(me)`가 좌측, Client 화면은 `Client(me)`가 좌측에 유지된다. |
| **Title Prototype UI** | `TitleSceneController`는 `TitleMainPanel`, multiplayer 선택 패널, join/lobby/wrong-key panel을 title flow 검증용으로 관리한다. source snapshot 보관은 `Assets/Scenes/merged/TitleScene.unity`, runtime target은 `Assets/Scenes/mutiplayer/TitleScene.unity`를 사용한다. runtime 시작은 `Text_PressAnyKey`를 먼저 보여주고 keyboard/mouse 입력 1회 후 panel을 연다. runtime root는 `TitleRuntimeRoot (1)`을 먼저 바인딩하고 없을 때만 `TitleRuntimeRoot`로 fallback한다. selected root는 Play Mode에서 center anchor + local zero 계약으로 정규화되고 root Animator는 비활성화해 quarter/right-half 표시 회귀를 막는다. `TitleMainPanel`은 Main state 진입 시 runtime 강제 재정렬 없이 scene 배치값을 유지한다. same prompt는 scene anchor/position 기준 하단 중앙 배치를 유지하고, recovered animation controller가 anchored position을 덮어쓰지 않도록 Animator를 비활성으로 유지한다. background layer는 `TitleCanvas/Background/Image` 오브젝트를 활성 상태로 유지하고 Animator만 비활성으로 잠가 child decorative mesh가 `Image_title (1)` 위치로 떠오르는 회귀를 막는다. |

### 3.5. Game Logic & Flow
| Component | Note |
| --- | --- |
| **Game Loop** | `TitleSceneController` -> `SceneLoader` -> `LoadingSceneController` -> `GameManager` 흐름으로 title, loading, gameplay, result를 분리한다. solo `GamePlay` target은 explicit full path(`Assets/Scenes/mutiplayer/GamePlayScene.unity`)를 사용한다. |
| **Scene Transition Contract** | target scene 예약/소비/완료 알림은 `SceneLoader`가 단일 진입점으로 관리한다. `LoadingSceneController`가 `SceneManager.LoadSceneAsync(...)`를 성공시키려면 target scene entry가 `ProjectSettings/EditorBuildSettings.asset`에서 enabled여야 한다. |
| **Restart & Result** | 전투 결과 처리와 current scene restart는 `GameManager`가 담당한다. solo는 기존 local `Health` death event path를 유지하고, multiplayer는 `MultiplayerBossAuthorityBridge.IsBossDead`로 `Victory`, active `MultiplayerPlayerAvatar` 둘 다 dead일 때만 `Defeated`를 판정한다. multiplayer gameplay scene 판정은 `GamePlayScene_Verify.unity`와 `GamePlayScene.unity` 두 경로를 모두 허용한다. result UI는 `GameOver_Panel` 아래 `Image_Win` / `Image_Lose` art contract를 토글하고, `Text_GameResult`/TMP label은 object를 유지한 채 `Victory`일 때 공백(`" "`), `Defeated`일 때 result message(솔로 `_defeatedText`, 멀티플레이 retry prompt)를 표시한다. defeat retry 입력(`Enter`)과 ready count consensus/latch 로직은 그대로 유지하며, `2/2`가 되면 Host는 short delay 뒤 `MultiplayerSessionService.RestartGameplayAsync()`로 NGO gameplay scene reload와 player respawn을 다시 시작한다. |

---

## 4. Related Documents

| Topic | Source of Truth |
| --- | --- |
| **Coding rules / Zero-GC / physics rules** | `docs/technical/Coding_Standard.md` |
| **Input packet / FSM flow / state transition detail** | `docs/technical/Input_FSM_Flow.md` |
| **Technical term reference** | `docs/technical/Technical_Glossary.md` |
| **AI workflow / plan-first / document sync** | `docs/AI_Maintenance_Guide.md` |
| **Multiplayer overall design** | `docs/technical/multiplayer/Multiplayer_Design.md` |
| **Player action authority** | `docs/technical/multiplayer/player/Multiplayer_Player_Action_Authority.md` |
| **Boss authority** | `docs/technical/multiplayer/boss/Multiplayer_Boss_Authority.md` |
| **Boss aggro rule** | `docs/technical/multiplayer/boss/Mutiplayer_Boss_Aggro.md` |
| **Daily implementation history** | `docs/Progress_Log/README.md` 및 각 일자 로그 |

이 문서는 architecture overview와 current runtime snapshot을 유지한다.
일자별 변화, temporary verify 메모, detailed rule dump는 전용 문서에 남긴다.

