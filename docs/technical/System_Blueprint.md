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
        +CaptureCurrentLocomotionState(...)
        +ApplyLocomotionState(...)
        +SimulateLocomotionTickFromCurrent(...)
        +RefreshLocalPresentationBindings()
        +GetPreferredCameraFollowPosition() Vector3
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
    MultiplayerPlayerAvatar --> PlayerController : configures runtime role
    MultiplayerPlayerAvatar --> LocalInputProvider : toggles local owner input
    MultiplayerPlayerAvatar --> MultiplayerBufferedInputProvider : writes remote owner input
    MultiplayerPlayerAvatar --> ClientToHostPlayerActionIntent : emits action edges
    MultiplayerPlayerAvatar --> HostPlayerActionValidator : validates dash/attack start
    MultiplayerPlayerAvatar --> HostPlayerReactionResolver : resolves host reaction
    MultiplayerPlayerAvatar o-- HostPlayerState : keeps host truth
    MultiplayerPlayerAvatar o-- HostToClientPlayerReactionSnapshot : caches latest reaction
    MultiplayerPlayerAvatar o-- MultiplayerLocomotionState : snapshots/replay
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
거리 기반 상태 전환과 비주얼 분리(BossVisual)가 적용된 보스 전용 구조입니다.

**관련 코드:**
*   **Controller**: `Assets/Scripts/Boss/BossController.cs`
*   **Visual**: `Assets/Scripts/Boss/BossVisual.cs`
*   **States**: `Assets/Scripts/Boss/BossFSM.cs` (모든 Boss State 클래스 포함)
*   **Attack Patterns**: `Assets/Scripts/Boss/Attacks/` (`IBossAttackPattern.cs`, `BasicAttackPattern.cs`, `LungeAttackPattern.cs`, `ProjectileAttackPattern.cs`, `AoEAttackPattern.cs`)
*   **Combat**: `Assets/Scripts/Common/Combat/Health.cs`, `Assets/Scripts/Common/Combat/DamageCaster.cs`, `Assets/Scripts/Common/Combat/BossHitBox.cs`

```mermaid
classDiagram
    direction TB

    %% Components
    class BossController {
        <<MonoBehaviour>>
        +MoveSpeed float
        +DetectionRange float
        +BasicAttackRangeOrigin Transform
        +BasicAttackRange float
        +LungeAttackRange float
        +ProjectileAttackRange float
        +AoEAttackRange float
        +ChaseReengageBuffer float
        +CanAttack bool
        +IsLocomotionVisualSuppressed bool
        -StateMachine~BossBaseState~ _stateMachine
        +BossVisual Visual
        +DamageCaster HeadDamageCaster
        +DamageCaster LungeDamageCaster
        +BasicAttackSettings BasicAttackSettings
        +LungeAttackSettings LungeAttackSettings
        +ProjectileAttackSettings ProjectileAttackSettings
        +BossProjectilePool ProjectilePool
        +Transform ProjectileSpawnPoint
        +SetLocomotionVisualSuppressed(bool)
        +GetPlanarDistanceToTarget() float
        +GetPlanarDistanceFromBasicAttackOriginToTarget() float
        +IsTargetInDetectionRange() bool
        +RefreshClosestLiveTarget(bool) void
        +GetPlanarDistance(Vector3, Vector3) float
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
    }

    class DamageCaster { +EnableHitbox(int) +DisableHitbox() +SetOwner(GameObject) }
    class Health { +CurrentHP int +OnDamageTaken Action~int~ +OnDeath Action }

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
    BossController --> DamageCaster : Controls

    BossBaseState --|> BaseState
    BossBaseState <|-- BossIdleState
    BossBaseState <|-- BossCombatState
    BossBaseState <|-- BossAttackState
    BossBaseState <|-- BossSearchingState
    BossBaseState <|-- BossHitState
    BossBaseState <|-- BossDeadState

    IDamageable <|.. BossHitBox : Implements
    BossHitBox --> Health : Delegates
    DamageCaster ..> IDamageable : Hits
```

**현재 배치 원칙:**
* `HeadDamageCaster`, `LungeDamageCaster`는 Boss 로직 계층(루트 또는 루트 하위 로직 오브젝트)에 둔다.
* `HeadDamageCasterPlace`, `BodyDamageCasterPlace`는 Visual/Bone 계층에 남기고 `_castCenter` 앵커로만 사용한다.
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
        -bool _damageWindowOpen
        +Enter(BossController)
        +Update(BossController) bool
        +Exit(BossController)
    }

    class BasicAttackSettings {
        +readyDuration float
        +readyNormalizedWindow Vector2
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
        +RestartCurrentScene()
    }

    %% Relationships
    TitleSceneController ..> SceneLoader : requests transition
    SceneLoader --> LoadingSceneController : reserves target
    LoadingSceneController ..> SceneLoader : consumes target/completes transition
    GameManager ..> SceneLoader : shares game flow context
```

`main` 기준 `TitleSceneController`는 `MultiplayerModePanel / HostCreatePanel / ClientJoinPanel / LobbyPanel / WrongKeyPopup`을 local prototype UI로 유지한다.
이 경로는 title flow/layout 검증과 menu state transition 확인용이며, real Relay/Lobby/NGO bootstrap은 `feature/multiplayer` 브랜치에서만 소유한다.

---

## 3. Data Rules & Coding Standards

### [Input System]

* **Packet Structure**: `PlayerInputData.cs`에 정의된 `PlayerInputPacket`을 사용한다.
* **Bit-Masking**: 버튼 입력은 `bool` 필드를 늘리지 않고 `InputFlag` 열거형과 비트 연산(`|`, `&`, `~`)을 통해 `byte buttons` 필드 하나로 처리한다.
* *Example*: `if (input.HasFlag(InputFlag.Dash)) ...`



### [Physics & Movement]

* **Rotation Logic**:
* `CameraRoot`: 플레이어 자식이 아닌 월드 앵커로 운용한다. `ThirdPersonCameraController.LateUpdate()`에서 위치/방향을 갱신한다.
* `lookYaw`, `lookPitch`: 마우스 기반 카메라 회전의 1차 입력(Primary)으로 사용한다.
* `Auto-Behind Assist`: 런타임 데이터는 유지하되, 현재 인스펙터에서는 숨김 처리한다.
* `Smoothing Rule`: `Position Smooth Time`/`Rotation Smooth Time` 데이터는 유지하되, 현재 인스펙터에서는 숨김 처리한다.
* `Inspector Tooltip Rule`: 카메라 튜닝 필드에는 쉬운 영어 툴팁을 제공해 파라미터 의미를 인스펙터에서 바로 이해할 수 있어야 한다.
* `moveDir`: **Character Body** 회전 및 이동용 (키보드 입력).
* 캐릭터 몸통은 카메라가 바라보는 방향(`cameraRoot.forward`)을 기준으로 이동 벡터를 변환해야 한다.
* **Boss Planar Distance Rule**: Boss의 감지/추적/공격 사거리 판정은 Y축을 제외한 수평(XZ) 거리 기준으로 계산한다.
* **Boss Pattern Range Rule**: 보스 공격 사거리는 패턴별 인스펙터 값(`Basic`, `Lunge`, `Projectile`, `AoE`)으로 분리하며, 패턴 선택 시 현재 거리에서 유효한 패턴만 후보로 포함한다.
* **Boss Basic Range Origin Rule**: Basic 공격 사거리 판정은 `basicAttackRangeOrigin` 기준점에서 타겟까지의 XZ 거리로 계산한다. 기준점이 비어 있으면 Boss Root를 폴백으로 사용한다. 기본 씬 설정은 `HeadDamageCasterPlace`를 사용한다.
* **Boss Basic Range Single-Source Rule**: Basic 공격의 조정 가능한 범위 값은 `HeadDamageCaster.radius` 하나만 사용한다. `BossController.BasicAttackRange`와 Basic range gizmo는 이 값을 직접 읽어 사거리 판정과 실제 타격 반경이 항상 같은 source를 공유해야 한다. 숨겨진 `basicAttackRange` 직렬화 필드는 `HeadDamageCaster`가 비어 있을 때만 legacy fallback으로 사용한다.
* **Boss Attack1 Ready Window Rule**: Attack1의 준비동작은 `BasicAttackSettings.readyDuration` + `readyNormalizedWindow`로 조절한다. 선택한 normalized slice는 해당 시간만큼 재생되도록 `Animator.speed`를 임시 보정하고, `HeadDamageCaster`는 준비 구간이 끝날 때까지 비활성 상태를 유지한다. 상태 종료/인터럽트 시에는 playback speed를 `1.0`으로 복구해야 한다.
* **Boss DamageCaster Ownership Rule**: Basic/Lunge `DamageCaster`는 `BossVisual` 자식 Bone에 직접 두지 않고 Boss 로직 계층에 둔다. 실제 판정 위치 추종은 `_castCenter`에 할당된 `HeadDamageCasterPlace`/`BodyDamageCasterPlace`가 담당한다.
* **Boss Phase1 Attack Priority Rule**: Phase1에서 Basic/Lunge 조건이 동시에 만족되면 Basic을 우선 선택한다. Lunge는 Basic 범위를 벗어났고 Lunge 범위는 만족할 때만 선택한다.
* **Boss Lunge Root Motion Relay Rule**: Lunge 이동은 `rushPhaseRatio/MoveRaw` 수동 전진이 아니라, Animator `OnAnimatorMove`의 루트모션 델타를 `BossController.ApplyLungeRootMotion(deltaPosition, normalizedTime)`으로 전달해 부모 루트를 이동시킨다. 활성화 구간은 `SetLungeRootMotionEnabled(true/false)`로 제한하며, 릴레이는 시작 시 `Visual` 로컬 기준 포즈를 캐시하고 `OnAnimatorMove`/종료 시 복원해 부모 루트와 자식 비주얼 좌표 분리를 방지한다. `ResolveAppliedDeltaPosition` 기본 경로는 XZ Animator 델타를 사용하고, 미소 프레임에서는 Visual 월드 델타 폴백을 사용한다.
* **Boss Lunge Motion Distribution Tuning Rule**: 실험 9에서는 Lunge `normalizedTime` 구간별 이동량 배분 보정을 위해 `LungeAttackSettings`의 `midBoost*`/`lateReduce*` 파라미터를 적용한다. `OnValidate`에서 시작/종료 구간은 `0~1`로 clamp하고, 종료값이 시작값보다 작으면 시작값으로 보정하며, 배수(`midBoostScale`, `lateReduceScale`)는 `0` 이상으로 강제한다.
* **Boss Lunge Damage Window Rule**: Lunge 판정 타이밍은 고정 상수 대신 `LungeAttackSettings.damageCastNormalizedWindow`를 사용한다. `Start/End`는 `0~1` 범위로 clamp하며, `LungeAttackPattern`은 해당 `normalizedTime` 구간에서만 `LungeDamageCaster`를 열고, 상태 종료 시점은 `1.0`을 유지한다.
* **Attack1 Inspector Ready Gauge Rule**: Attack1의 준비 시작/종료는 기본 인스펙터에서 2핸들 MinMax 슬라이더와 `Start/End` float field로 노출한다. 디자이너는 `normalizedTime 0~1` 범위 안에서 어떤 bite slice를 ready motion으로 쓸지 직접 튜닝할 수 있어야 한다.
* **Boss Attack2 Phase Marker Rule**: Attack2의 구간 분리(`Windup -> PreLaunch -> Launch -> Airborne -> Land`)는 애니메이션 이벤트(예: `PreLaunchStart`, `Launch`, `Land`)를 우선 기준으로 사용한다. 이벤트는 단일 슬롯이 아닌 큐(Queue)로 누적/소비해 프레임 드랍 시 마커 유실을 방지한다. 이벤트 누락 시에는 `normalizedTime` 임계값 폴백으로 동일 전이를 보장하고, 누락 마커는 `MarkerPathWarn` 로그로 추적한다.
* **Boss Attack2 Launch Guard Rule**: Attack2의 `launchNormalizedTime`/`landSnapNormalizedTime`은 종료 시점(`1.0`) 이전 상한(`0.98`)으로 강제한다. 설정값이 상한을 넘으면 `OnValidate`에서 자동 보정해 Launch 영구 미진입 구성을 방지한다. Relay는 AnimEvent 누락 시 `AnimEventSynth`로 동일 마커를 합성 큐잉한다.
* **Boss Attack2 Landing Snap Rule**: `Windup/PreLaunch` 동안에는 `stepOffset`을 `0`으로 낮추고, 루트모션 Y를 차단한 뒤 `RaycastNonAlloc` 기반 Ground 스냅으로 목표 높이를 유지한다. `Launch` 프레임에서 ground lock을 해제하고 `stepOffset`을 원복한 뒤 루트모션 Y를 허용한다. `Land` 이벤트를 우선 기준으로 1회 스냅을 적용하고, 이벤트 누락 시에는 `landSnapNormalizedTime` 폴백으로 동일 스냅 경로를 보장한다.
* **Attack2 Spatial Probe Rule**: `[Attack2Landing][SpatialProbe]` 로그는 `player`, `boss`, `visual`, `red(Boss/Visual/Red)` 좌표와 상대 벡터(`player-boss`, `player-red`, `boss-red`, `visualInBoss`, `redInBoss`) 및 `redPath`를 함께 출력한다. GroundLock 시작/해제, 착지 스냅, 모션 종료와 RootMotion 로그에 결합해 좌표계 이탈 원인을 프레임 단위로 추적한다.
* **Attack2 Inspector Damage Window Rule**: Attack2의 피격 시작/종료는 기본 인스펙터에서 2핸들 MinMax 슬라이더와 `Start/End` float field로 노출한다. 디자이너는 `normalizedTime 0~1` 전체 구간 안에서 DamageCaster 활성 윈도우를 직접 튜닝할 수 있어야 한다.
* **Attack2 Player Y Trace Rule**: 플레이어는 Attack2 근접/피격/스턴 이동 동안 `[Attack2PlayerY]` 로그를 출력해 `transform.y`, `bottomY/topY`, `grounded`, `CharacterController.velocity.y`, `CollisionFlags`, 현재 상태명, 보스 Attack2 `normalizedTime`을 함께 추적한다.
* **Attack2 Gizmo Feature Toggle Rule**: `BossController`의 `OnDrawGizmosSelected`는 단일 고정 그리기 대신 기능별 토글(`showAttackRangesGizmo`, `showDetectionRangeGizmo`, `showAttack2GroundProbeGizmo`, `showAttack2SnapWindowGizmo`, `showAttack2SpatialLineGizmo`)로 분리한다. Attack2 디버깅 시 필요한 기즈모만 선택적으로 표시해 시야 오염과 튜닝 혼선을 줄인다.
* **Attack2 Recovery Priority Rule**: Attack2 개선에서 디버그 계층(인스펙터/기즈모/로그)을 추가했음에도 핵심 증상(타겟 근접 도약 실패, 원위치 당김)이 유지되면, 추가 튜닝을 누적하지 않고 마지막 안정 커밋 기준으로 이동 경로를 먼저 회귀한다.
* **Boss Detection Trigger Rule**: Idle/Searching에서 Combat 전환(스크림 인트로 진입)은 `IsTargetInDetectionRange()` 기준으로 즉시 수행한다. 현재 보스 감지는 장애물/시야선(LOS) 판정을 사용하지 않는다.
* **Boss Closest Live Target Rule**: current verify multiplayer path에서는 boss가 non-attack state check 진입 시 `RefreshClosestLiveTarget(...)`로 가장 가까운 살아있는 `PlayerController`를 다시 선택한다. current rule은 temporary verify unblock용이며, later aggro/contribution system 이전 단계의 nearest-target fallback이다.
* **Boss Chase Hysteresis**: 단일 임계값 대신 "현재 페이즈에서 활성화된 패턴 중 최대 사거리"(해제)와 `최대 사거리 + ChaseReengageBuffer`(재진입) 이중 임계값을 사용해 경계 왕복 지터를 완화한다.
* **Player Stun Hit Classification Rule**: 플레이어 피격은 `BossAttackHitType` 메타데이터로 분기한다. `Attack1`은 일반 피격(데미지+Hit), `Attack2`는 데미지 적용 후 스턴(`damage + stun`), `Attack3/Attack4 Projectile`은 `Projectile Count Timer` 내 1타 일반 피격/2타 스턴으로 처리한다.
* **Player Stun Invulnerability Rule**: 스턴 중 + 스턴 종료 후 `postStunInvulDuration` 동안 플레이어는 무적이며, 이 구간에는 추가 스턴 재트리거를 허용하지 않는다. 후속 무적 시각 피드백은 `BlinkWhiteEffect` 컴포넌트가 담당하며, `_BlinkWhite` 파라미터를 통해 흰색 점멸을 출력한다(`_BlinkWhite=1` 구간은 조명 영향 무시).
* **Attack4 Warning Single-Source Rule**: Pattern 4 AoE의 시간 동기화 기준은 `warningDuration` 단일 값이다. circle warning 종료(fully red 시작)와 fire projectile impact marker 시간은 같은 `warningDuration`을 사용해야 한다.
* **Attack4 Fully-Red One-Hit Rule**: Pattern 4 AoE circle은 warning 종료(fully red) 이후 active window 동안 반경 판정을 수행한다. 각 circle은 같은 target에게 최대 1회만 타격하며, fully red 구간에 늦게 진입한 target도 1회 타격 대상이 된다. `BossAttackHitResolution.Ignored`(invul) 결과는 소비 처리하지 않는다.
* **Attack4 Visual Warning Bias Rule**: AoE runtime fallback 디스크는 UX 경고 강화를 위해 시각 반경을 데미지 반경보다 약간 크게 표시할 수 있다. 현재 기본값은 `fallbackRadiusToScaleMultiplier = 1.2`이며, 의도는 "데미지보다 넓게 경고"다.


* **Optimization**:
* 물리 판정 시 `Physics.OverlapSphere` 금지 → **`Physics.OverlapSphereNonAlloc`** 사용.
* 모든 물리 쿼리 결과 배열(`Collider[]`)은 클래스 멤버 변수로 미리 할당(Pre-allocate)하여 재사용한다.



### [FSM Implementation Guide]

* **Role of Controller**: `PlayerController`는 `CharacterController.Move()`와 같은 실제 물리 실행 메서드만 `public`으로 열어두고, '어떻게' 움직일지 결정하는 로직은 `State` 클래스에 위임한다.
* **State Transition**: 상태 전환은 `StateMachine.ChangeState()`를 통해서만 이루어져야 한다.
* **Controller Member Layout Rule**: 컨트롤러 클래스는 필드 선언을 먼저 모아 배치하고, `OnValidate`를 포함한 메서드는 필드 선언 이후에 배치해 선언부와 실행부를 분리한다.

---

## 4. Implementation Notes

### 4.1. Core Systems & Input
| Component | Note |
| --- | --- |
| **IInputProvider** | `LocalInputProvider.cs` 구현 완료. |
| **Input Packet** | `PlayerInputPacket` (Bit-packing) 적용 완료. |
| **StateMachine** | `BossRaid.Patterns` 네임스페이스 적용 및 구현 완료. |
| **Physics System** | `NonAlloc` 물리 판정(OverlapSphere) 및 최적화 완료. |
| **Object Pooling** | `BossProjectilePool` 기반 투사체 재사용(Prewarm/Max/Expand) 구현 완료. |
| **Third-Person Camera Module** | `Assets/Scripts/Camera/ThirdPersonCameraController.cs`를 메인 카메라 측 모듈로 분리해 카메라 설정을 카메라 오브젝트로 이동했다. |
| **Package Baseline** | Unity 2022.3 기준 shared `main` baseline은 `URP/VFX 14.0.12`, `TMP`, `2D Sprite`, `FBX`만 유지하고, UGS/NGO/Relay/Lobby package set은 `feature/multiplayer` 브랜치에서만 소유한다. |
| **External Asset Distribution Policy** | 프로젝트 소유 코드/씬/프리팹/설정만 Git으로 추적하고, 서드파티 imported pack은 Google Drive 배포본을 기준선으로 관리한다. 현재 외부 배포 pack은 `Assets/ApocalypticEnvironment`, `Assets/CombatGirlsCharacterPack`, `Assets/Fantasy Skybox FREE`, `Assets/FourEvilDragonsPBR`, `Assets/Hits Effects FREE`, `Assets/InfographicElements_UI`, `Assets/Lunar Landscape 3D`, `Assets/Map`, `Assets/PixPlays`, `Assets/UNI VFX`다. Unity 참조 안정성을 위해 Drive 배포본도 원본 에셋과 `.meta`를 같은 상대 경로로 함께 보관한다. |

### 4.2. Player System
| Component | Note |
| --- | --- |
| **Movement Logic** | `MoveState`로 로직 이관 완료. |
| **Dash Logic** | Cooldown 및 Edge-triggering 기능 포함 구현 완료. |
| **Jump Logic** | `JumpState` 구현 완료. 현재 게임 디자인 기준 점프 입력 전환은 비활성(주석/F10 유지) 상태이며 필요 시 재활성 가능. |
| **Camera Logic** | `ThirdPersonCameraController`가 메인 카메라에서 `lookYaw/lookPitch`를 1차 입력으로 처리하고, `CameraRoot`를 관리한다. smoothing/auto-behind 데이터는 유지하되 현재 인스펙터 노출은 숨김 처리되어 있다. 기본 hidden smoothing 값은 `positionSmoothTime = 0.01f`, `rotationSmoothTime = 0.01f`다. current follow-up 기준 predicted owner path는 별도 hidden tuning(`usePredictedOwnerTightFollow`, `predictedOwnerPositionSmoothTime`, `predictedOwnerRotationSmoothTime`)을 사용하고, 기본값은 `0 / 0` direct orbit follow다. |
| **Attack Logic** | `AttackState` 구현 완료. 콤보/캔슬/개별 데미지 지원. 상태 전환 시 `AttackState.Exit()`에서 히트박스를 강제 종료해 잔존 판정을 방지하며, `DamageCaster`는 0 데미지 윈도우를 무시한다. |
| **Hit/Damage System** | `IDamageable`, `DamageCaster`, `Health` + `IBossAttackHitReceiver` 기반 보스 공격 메타데이터 라우팅 구현 완료. 플레이어는 `StunState`/`Projectile Count Timer`/후속 무적 규칙을 사용하고, 보스는 공격 준비/실행 중 피격 모션을 무시한다. 플레이어/보스의 점멸은 공용 `BlinkWhiteEffect` 컴포넌트(`Assets/Scripts/Common/Visual/BlinkWhiteEffect.cs`)가 담당하며 `_BlinkWhite` 셰이더(`Assets/Shaders/BlinkWhiteLit.shader`)를 `MaterialPropertyBlock`으로 제어한다. |
| **Asset Integration** | `PlayerAnimator`의 `Hit/Attack1/2/3/Die` 상태 모션 재연결 완료(2026-02-21). |
| **Environment Fix Guard (환경 오류 복구)** | `Assets/Editor/PlayerAnimatorGuard.cs`로 환경 변경 시 발생하는 Animator 참조 오류를 자동 복구/검증한다. 필수 state/motion + 파라미터(`Speed` Float, `Hit` Trigger) 누락 점검, 모든 Layer + 중첩 StateMachine 재귀 순회, Locomotion BlendTree 자식 모션 검증, 중복 상태명 경고, 로드/임포트/이동/메뉴 경로를 지원하며 `Hit` 상태명은 `PlayerController.ANIM_STATE_HIT` 상수를 공용 참조한다. 추가로 `Attack1/2/3` 클립의 `OnHitStart/OnHitEnd` 이벤트 자동 보정 및 누락/순서 검증, `Tools/Validation/Fix Player Attack Events` 메뉴를 포함한다. |
| **Multiplayer Ownership Scaffold** | `feature/multiplayer` 기준 `MultiplayerPlayerAvatar`, `MultiplayerBufferedInputProvider`, `MultiplayerLocalPlayerRegistry`, `MultiplayerGameplaySceneCoordinator`를 추가했다. scene cleanup은 `SceneManager.sceneLoaded` + avatar spawn fallback으로 보강했고, legacy scene `Player`는 main camera 분리 후 runtime hierarchy에서 제거한다. network player runtime name은 `hostPlayer` / `clientPlayer`로 고정하고, spawn slot은 Host/Client 기준으로 좌우 분리한다. 카메라/HUD는 local-owned player에만 다시 bind하며 backlog `4.1` 구현 기준은 충족했다. 2026-04-02 follow-up에서는 verify multiplayer scene의 boss가 destroyed legacy player 참조에 남지 않도록, authoritative player spawn 직후 spawned avatar 중 live target으로 다시 rebind하는 runtime target handoff도 추가했다. 2026-04-03 follow-up에서는 client side boss authority를 display-only로 더 좁혀 local `CharacterController`도 끄고, Host snapshot만 따르는 verify presentation path를 보강했다. |
| **Player Action Authority Bootstrap (2026-04-01)** | `ClientToHostPlayerActionIntent`와 `HostPlayerActionValidator`를 추가해 locomotion packet과 분리된 `dash / Attack1` action-intent uplink를 먼저 만들었다. `MultiplayerPlayerAvatar`는 owner action edge를 reliable ServerRpc로 Host에 보내고, Host local player도 같은 validator path를 network hop 없이 탄다. current slice는 `step 1~3` 범위만 반영하므로 validated action 실행/복제는 아직 붙이지 않았고, `MultiplayerActionIntentTrace` 기준으로 Host route를 먼저 고정했다. |
| **Player Action Authority Host Slice (2026-04-01)** | `HostPlayerState`, `HostToClientPlayerReactionSnapshot`, `HostPlayerReactionResolver`를 추가해 `step 4~6` Host truth layer를 먼저 고정했다. `MultiplayerPlayerAvatar`는 server spawn 시 Host state를 seed하고, accepted action을 state에 기록하며, `PlayerController`의 `BossAttackResolved` / `AttackDamageResolved` event를 받아 one-shot reaction snapshot과 `server tick` 기준 `raw hit log`를 만든다. current follow-up 기준 remote `Dash/Attack` validation primary path는 separate action RPC가 아니라 `SubmitOwnerInputServerRpc(...)` receive 시점의 `server-buffer` edge detect다. `rpc-received` 는 extra diagnostic trace로만 남기고, Host truth는 `buffer-observe -> validate -> host-state` 흐름으로 고정했다. current slice는 아직 `Replicator / Applier` 이전 단계이므로 snapshot 생성과 Host log까지만 연결했고, owner/remote client apply는 다음 단계로 남겨 둔다. |
| **Player Action Authority Health Gate (2026-04-02)** | `Health`는 solo-safe default를 유지한 채 runtime write gate를 가지게 됐고, `MultiplayerPlayerAvatar`가 role configure 시 Host-owned player / Host authority replica에는 writable authority를, client owner / client replica에는 read-only authority를 적용한다. current `step 7` 범위는 HP replication이 아니라 `TakeDamage` / `Heal` 최종 쓰기 경로를 Host 쪽으로 먼저 잠그는 것이며, owner/remote client apply와 HUD authoritative sync는 이후 `Replicator / Applier` 단계에서 이어진다. |
| **Boss Client Presentation Mirror (2026-04-03)** | current verify multiplayer path에는 boss 전용 network actor가 아직 없으므로, `MultiplayerPlayerAvatar`가 Host authority replica에서 boss `position / rotation / animator state / normalized time / Speed parameter`를 owner-targeted `ClientRpc`로 보낸다. client owner는 local boss logic가 꺼진 상태에서 이 snapshot만 apply해 movement와 current attack animation state를 화면에 표시한다. scope는 display-only mirror이며, projectile/AoE object replication과 gameplay truth는 아직 Host 쪽에 남는다. |
| **Boss Closest Live Player Retarget (2026-04-03)** | `BossController`는 non-attack state update에서 `RefreshClosestLiveTarget(...)`를 호출해 현재 씬의 살아있는 player 중 가장 가까운 대상을 다시 고른다. current refresh는 `0.1s` throttled nearest-target fallback이며, spawn-time one-shot rebind만으로 host detection이 막히는 verify 문제를 푸는 temporary rule이다. full aggro/contribution priority는 이후 단계로 남긴다. |
| **Multiplayer Player HUD Replica (2026-04-03)** | `MultiplayerPlayerAvatar`는 server-authoritative `current/max HP`를 HUD 전용 `NetworkVariable<int>` pair로 유지하고, local-owned avatar가 자기 화면의 `CombatHUDController`에 main/partner player HP를 모두 그린다. `PlayerController.InitializeCombatHUD()`는 solo에서는 기존 local `Health` source를 유지하지만, active multiplayer session에서는 player source를 `null`로 두는 manual HUD mode로 전환해 stale local `Health`가 Host truth HUD를 덮어쓰지 못하게 한다. viewer-side label rule은 Host 화면 `Host(me)` / `Client`, Client 화면 `Client(me)` / `Host`다. |
| **4.4 Reference-Based Direction (Current)** | current `4.4` direction은 `Host authority + local free-move prediction + lazy boundary correction + shared CharacterController movement truth`다. `1~2 sec delay`, client authority, half prediction(`client predict + Host correction only`), separate custom locomotion motor, deprecated `LookOnly` fallback은 current active answer로 채택하지 않는다. `Boss Room`은 historical masking reference로만 남기고, current code path는 owner free-move 화면을 calm하게 유지하는 `single-root presentation` 쪽을 유지한다. |
| **4.4 Multiplayer Movement (Current)** | current runtime code는 `PredictionReconciliation` 한 경로를 유지하되, owner free move correction policy를 `lazy boundary correction` 으로 좁혔다. client owner는 `PredictedLocomotion` mode에서 locomotion prediction을 수행하고, Host authority replica는 shared `PlayerLocomotionCore`를 사용해 같은 move / rotate / gravity / grounded rule로 authoritative locomotion state를 계산한다. 2026-04-01 follow-up 기준 shared locomotion state는 `dash timer / dash cooldown / last buttons / dash active flag` 도 함께 들고 가며, `walk + dash` 를 같은 shared `CharacterController` sim 안에서 계산한다. owner는 first Host authoritative locomotion state를 baseline으로 받은 뒤, locomotion snapshot은 우선 `shadow authoritative state` 로 저장하고 drift만 계산한다. immediate sync는 `fallback(비이동 구간)`, `boundary sync`, `hard fail`, `stale sync`, `idleSettle` 에서 수행한다. current active gate는 `Dash` 는 predicted locomotion path에 남기고, `Attack / Jump` 같은 non-locomotion input만 Host authoritative `Full` simulation fallback으로 내린다. current 1차 threshold는 free-move ignore `0.20m / 10deg`, hard-fail `0.65m / 25deg`, stale snapshot `300ms`, idle-settle planar speed `<= 0.05` 다. 2026-03-30 smoke test에서 old shadow snapshot compare 기반 `hardFailShadow` 가 too eager하다는 문제가 확인됐고, current follow-up code에서는 그 tick-side path를 제거했다. 후속으로 medium drift가 stop state에서 오래 남는 로그를 보고 `idleSettle` 을 추가했다. 그 다음 측정에서는 correction spam보다 `raw predicted root tick-step` 이 owner 화면 jitter의 중심임이 확인됐고, current code는 free move owner body와 camera가 같은 `render proxy` surface를 보도록 다시 연결했다. current 추가 follow-up으로 predicted owner camera orbit은 render proxy를 다시 `SmoothDamp` 하지 않고 direct follow를 기본값으로 사용한다. 2026-04-01 dash smoothing follow-up에서는 dash 시작/진행도 이 same predicted path 안에 남도록 바꿨고, presentation driver는 dash 중 predicted transition을 즉시 snap해 dash root를 늦게 쫓는 느낌을 줄였다. 같은 날 후속 fix에서는 dash 종료/owner replay handoff의 animator `Speed` 도 actual planar speed를 함께 보도록 바꿔 reverse turn 직후 tiny idle flash를 줄였다. 2026-03-31 fix에서는 Host authority replica가 move-only mode로 다시 들어올 때 remote avatar animator를 `Locomotion` 으로 되돌리고, fixed-tick locomotion sim에서도 `Speed` 를 함께 갱신해 host 화면에서 client idle/walk가 빠지지 않게 보강했다. `NetworkConfig.TickRate = 60` 을 유지한다. runtime 재검증은 이 shared render proxy + direct orbit follow + predicted dash 기준으로 다시 수행해야 한다. |
| **PlayerController Multiplayer Diet (2026-03-30)** | `PlayerController`는 solo FSM/HUD/attack 중심 orchestration은 유지하되, shared locomotion simulation은 `Assets/Scripts/Player/PlayerLocomotionCore.cs`로, local multiplayer presentation은 `Assets/Scripts/Multiplayer/Gameplay/MultiplayerPlayerPresentationDriver.cs`로 위임한다. current lazy-correction follow-up 기준 `MultiplayerPlayerPresentationDriver`는 free move owner 화면용 `render proxy` 를 관리하고, visual child와 camera가 같은 proxy surface를 보도록 연결한다. `MultiplayerPlayerAvatar`와 `ThirdPersonCameraController`는 기존 `PlayerController` public API를 그대로 사용하고, controller 내부에서 compatibility wrapper만 유지해 call site churn을 줄였다. |
| **4.4 Movement Trace Cleanup (2026-04-01)** | `[MultiplayerClientMoveTrace]`, `[MultiplayerPredictedRenderTrace]`, `[MultiplayerCameraFollowTrace]` movement debug line은 current step `1~3` 목적에 비해 noise가 커서 제거했다. current multiplayer runtime에서는 action authority용 `MultiplayerActionIntentTrace` 와 session continuity용 `MultiplayerConnectionDebug` 만 남겨 두고, old movement trace 해석은 historical progress log와 investigation 문서에서만 참고한다. |
| **4.4 Connection Debug (2026-04-02)** | `MultiplayerSessionService`가 `[MultiplayerConnectionDebug]` structured logger를 사용해 `peer-connect`, `peer-disconnect`, `state-change`, `gameplay-sync-complete`, `pulse` event를 한 prefix로 남긴다. line은 current app role(`editor-host`, `build-client` 같은 `editor/build + host/client` 조합), `state`, `scene`, `localClientId`, `isServer`, `isClient`, `isConnected`, `isListening`, `shutdown`, `hostPeerCount`, `lobbyPlayers`를 함께 싣는다. 2026-04-02 follow-up 기준 Host state flow는 `LobbyActive -> StartingGameplay -> InGameplay` 로 나뉘며, `StartingGameplay` 는 scene handoff 동안만 사용하고 gameplay sync 완료 뒤에는 `InGameplay` 로 넘어가 late disconnect를 startup failure와 분리한다. disconnect logger는 exact edge를 보장하기 위해 `peer-disconnect` minimal line을 callback entry에서 먼저 남기고, avatar profile dump는 `peer-disconnect-detail` line으로 분리하며, shutdown 직전에는 cached snapshot 기반 `peer-disconnect-fallback` line을 한 번 더 남긴다. avatar 자체도 `avatar-profile-baseline` / `avatar-profile-transition` event로 `idle-only`, `idle-walk-only`, `action-observed` 진행을 미리 남겨 `idle/walk-only` run 여부를 disconnect 전부터 증명하게 한다. |

참조 로그: `docs/Progress_Log/2026-03-25.md`
참조 로그: `docs/Progress_Log/2026-03-26.md`
참조 로그: `docs/Progress_Log/2026-03-30.md`
참조 로그: `docs/Progress_Log/2026-03-31.md`
참조 로그: `docs/Progress_Log/2026-04-01.md`
참조 로그: `docs/Progress_Log/2026-04-02.md`
참조 로그: `docs/Progress_Log/2026-04-03.md`

### 4.3. Boss System (The Dragon)
| Component | Note |
| --- | --- |
| **Boss Logic (FSM)** | `BossController` 상태 머신 (Idle, Combat, Searching, Dead) |
| **Boss Sensors** | `IsTargetInDetectionRange`(XZ 거리 기반) 단일 규칙으로 Idle/Searching 전투 진입을 처리한다. current verify multiplayer follow-up에서는 non-attack state마다 `RefreshClosestLiveTarget(...)`가 가장 가까운 살아있는 player를 다시 고른 뒤 이 감지 규칙을 평가한다. 장애물 LOS 센서는 제거됨 |
| **Boss Navigation** | `MoveTo` (추적 이동) 및 `RotateTowards` (회전) 로직 + AoE 공중 연출 중 Locomotion 시각 잠금 가드 + `ChaseReengageBuffer` 기반 히스테리시스 추적 |
| **Boss Visuals** | 구조 분리 및 Dragon Asset(Animator/BlendTree) 통합 완료. `PlayFlyForward` 폴백을 비행 계열로 정리해 Walk 혼입 방지. |
| **Boss Combat** | `Pattern 1`(Basic), `Pattern 2`(Lunge), `Pattern 3`(Projectile: Flame Attack + Homing + Vertical Follow + VFX create/hit + hitReturnDelay + postFireRecovery/exitNormalizedTime) 완료. 패턴별 공격 사거리 분리(`Basic/Lunge/Projectile/AoE`) 및 거리 기반 패턴 후보 필터링, 최대 사거리 기반 추적 히스테리시스 반영. Basic 사거리 기준점은 `basicAttackRangeOrigin`(기본 씬: `HeadDamageCasterPlace`)으로 분리되었고, Attack1의 editable range source는 `HeadDamageCaster.radius`로 단일화됐다. `BossController.basicAttackRange`는 인스펙터에서 숨기고 legacy fallback 값으로만 유지한다. Attack1은 `BasicAttackSettings.readyDuration` + `readyNormalizedWindow`를 사용해 bite 준비 slice를 재생 시간 기준으로 튜닝하며, 준비 구간 동안 `HeadDamageCaster`를 닫고 준비 종료 후에만 판정을 연다. Boss 공격 `DamageCaster`는 로직 계층에 두고, `HeadDamageCasterPlace`/`BodyDamageCasterPlace`를 `_castCenter` 앵커로 사용한다. Lunge는 루트모션 브리지(`OnAnimatorMove -> ApplyLungeRootMotion`) + 시작 방향 고정 경로를 사용하며, 피격 활성 구간은 인스펙터 `damageCastNormalizedWindow`로 start/end를 튜닝한다. Phase1에서는 Basic/Lunge 동시 충족 시 Basic 우선 규칙을 적용한다. `Pattern 4`(AoE)는 fully red active window 반경 판정 + circle당 target 1회 타격 규칙으로 진행 중이다(늦게 진입한 target 포함, invul ignore는 소비하지 않음). |
| **Attack2 PreLaunch Ground Lock** | `LungeAttackPattern` 구간 분기(`Windup/PreLaunch/Launch/Airborne/Land`) 및 `BossController` ground lock/stepOffset 제어, 마커 큐 누적/소비(`PreLaunchStart/Launch/Land`) + `normalizedTime` 폴백까지 코드 반영 완료. `launch/land` 정규화 시점은 `0.98` 상한으로 강제해 종료 전 Launch 미진입 구성을 차단하고, Relay의 `AnimEventSynth`로 이벤트 누락 시 동일 마커를 합성한다. 루트모션 적용은 Animator `deltaPosition` 기반 단일 추종을 유지한다. `[Attack2Landing]` 로그는 `MarkerPathWarn`, `GroundSnapMiss`, `GroundSnapSkipMaxDistance`, `RootMotionMove/RootMotionRelayProbe`, `SpatialProbe(player/boss/visual/red 좌표)`를 포함해 실패 원인/좌표 경로를 추적한다. 플레이어 쪽 `[Attack2PlayerY]` 로그도 추가돼 근접 피격 시 Y/grounded/충돌 플래그를 함께 추적한다. Unity 실플레이 회귀 검증(머리 탑승/발 꺼짐/착지 정렬)은 남아 있다. |
| **Attack2 Inspector & Gizmo UX** | Attack2 `damageCastNormalizedWindow`가 기본 인스펙터에서 2핸들 게이지와 `Start/End` 숫자 필드로 노출된다. 판정 활성 시점 튜닝 UX는 반영됐고, broader Attack2 전용 timeline/gizmo 정리는 후속 작업으로 남아 있다. |
| **Attack2 Repro Harness (Test Scene)** | `GamePlayScene_TestResult` 로드 시 `BossAttack2ReproHarness`를 자동 생성해 플레이어를 보스 전방 재현 위치로 고정한다. 수동 착지지점 배치 없이 Attack2 위로 올라감 회귀를 반복 재현할 수 있다. |

### 4.4. User Interface (UI)
| Component | Note |
| --- | --- |
| **UI System** | 전투 HUD 배치 + `CombatHUDController` 연동 완료. solo에서는 `Health.OnDamageTaken/OnDeath` 이벤트와 `LateUpdate()` fallback refresh로 플레이어/보스 HP Fill을 즉시 갱신하고, `DamageCaster.OnAttackWindowResolved` 결과를 `HIT + 피해량` 고정형 피드백(스케일 강조 후 짧은 페이드 아웃)으로 표시한다. 이름 라벨 및 `ShowHud(bool)` 기반 전체 표시 제어를 포함한다. `PartnerHUD_Panel`은 기본 hidden으로 시작하지만, active multiplayer session에서는 local-owned `MultiplayerPlayerAvatar`가 viewer-side HUD mapper로 main/partner player HP/name을 모두 다시 그린다. 이때 `PlayerController.InitializeCombatHUD()`는 player source를 manual mode(`Initialize(null, bossHealth)`)로 전환해 stale local player `Health`가 HUD를 덮어쓰지 못하게 하며, name rule은 Host 화면 `Host(me)` / `Client`, Client 화면 `Client(me)` / `Host`다. combo UI(`Text_Combo`)도 기본 hidden 상태를 유지하며, `AttackState.StartComboStep()`은 `PlayerController.SetPendingComboHudStep(step)`로 현재 단계만 준비하고, 실제 open은 `DamageCaster.OnAttackHitConfirmed` -> `PlayerController.ShowComboHud(step)` -> `CombatHUDController.ShowCombo(step)` 경로에서만 수행한다. `AttackState.Exit()` / `InitializeCombatHUD()` / `ShowHud(false)`는 `HideCombo()`로 stale combo UI를 정리한다. `GameManager`는 scene binding이 비어 있어도 `GameOver_Panel` / `GameResult` text를 runtime에서 재탐색한다. |
| **Title Prototype UI** | `TitleSceneController`가 기존 캔버스 위에 `TitleMainPanel / MultiplayerModePanel / HostCreatePanel / ClientJoinPanel / LobbyPanel / WrongKeyPopup`을 구성한다. `main`에서는 이 패널들이 local prototype flow로만 동작하며, Host path는 auto room title + fake join code + start unlock timer를 사용한다. real bootstrap/session/network transport는 이 브랜치에 두지 않고 `feature/multiplayer`에서만 유지한다. |

### 4.5. Game Logic & Flow
| Component | Note |
| --- | --- |
| **Game Loop** | `TitleSceneController`가 `Solo Play / Multi Play` 버튼 기반 시작 흐름을 관리하고, `SceneLoader` + `LoadingSceneController` 경유 전투 진입/`GameManager` 결과 처리까지 연결한다. `Multi Play` path는 shared `main`에서 local UI prototype으로만 남아 있으며, host/client panel state와 wrong-key popup UX를 검증할 수 있다. real multiplayer runtime scene routing과 service bootstrap은 `feature/multiplayer`가 소유한다. |

### 4.6. Branch Isolation Policy
| Component | Note |
| --- | --- |
| **Shared Main Baseline** | `main`은 UI/art/solo-safe shared branch다. 이 baseline은 `Assets/Scripts/Multiplayer/**`, duplicated multiplayer scenes, `Unity Services` / `Relay` / `Lobby` / `Netcode` package set 없이도 compile/run 가능해야 한다. shared art import에 필요한 `2D Sprite`, `FBX` package add-on은 허용한다. |
| **Multiplayer Ownership** | real multiplayer runtime, duplicated scenes, network prefabs, UGS/NGO package set, partner HUD activation은 `feature/multiplayer` 브랜치가 단일 owner다. shared UI/art 변경은 먼저 `main`에 합치고, `feature/multiplayer`가 이후 `main`을 merge/pull하는 흐름을 기본값으로 한다. |
| **Solo Branch Base** | `feature/solo-play`는 latest shared `main`에서 분기하는 safe gameplay branch다. solo-specific gameplay는 여기서 진행하되, shared UI/art asset은 계속 `main`을 integration point로 사용한다. |

참조 로그: `docs/Progress_Log/2026-03-24.md`

---

### 💡 Antigravity Prompting Guide (Plan-First)

이 파일을 기반으로 AI에게 작업을 지시할 때는 **구현 요청 전 계획서 승인**을 먼저 진행합니다.

#### 권장 요청 순서

1. 계획서 요청: 설계/영향 범위/검증 기준을 먼저 받는다.
2. 승인 응답: 계획서 확인 후 승인 또는 수정 요청을 준다.
3. 구현 요청: 승인된 계획서 기준으로만 구현 + 문서 동기화를 요청한다.

#### 계획서 최소 항목

| 항목 | 설명 |
| --- | --- |
| 목표 | 이번 변경으로 달성할 기능/문제 해결 목표 |
| 영향 클래스/파일 | 수정 대상과 연관 컴포넌트 |
| 설계 근거 | `System_Blueprint`의 어떤 원칙/구조를 따르는지 |
| 리스크 | 회귀 가능성, 충돌 가능성, 범위 외 영향 |
| 검증 포인트 | 테스트/수동 검증 시나리오와 성공 기준 |
| 문서 동기화 계획 | `docs/Progress_Log/YYYY-MM-DD.md` 기준 로그 지정 + `System_Blueprint`, `Technical_Glossary` 반영 계획 |

#### Progress_Log 추적 메모 규칙

1. 구현 현황/규칙 문구를 수정할 때는 근거 로그 파일을 1개 이상 지정한다.
2. 완료 보고에 `참조 로그: docs/Progress_Log/YYYY-MM-DD.md`를 남겨 변경 근거를 추적 가능하게 유지한다.

#### 프롬프트 예시

> "System_Blueprint.md를 기준으로 신규 기능 구현 계획서를 먼저 작성해줘. 영향 클래스, 리스크, 검증 포인트를 포함하고 승인 전에는 코드를 수정하지 마."

> "계획서 승인. 승인된 범위대로 구현하고, 완료 후 Progress_Log 기술적 고려에 [무엇을 발견/무엇을 수정/왜 그렇게 판단] 3항목을 남겨줘."




### 4.7. Compatibility Note (Unity 2022)
| Component | Note |
| --- | --- |
| **AoE Heading Sampling** | AoEAttackPattern의 타겟 속도 샘플링은 Unity 2022 기준 Rigidbody.velocity를 사용한다. |
| **Editor Assembly Anchor** | Assets/Editor/EditorAssemblyAnchor.cs를 통해 에디터 전용 어셈블리 생성 경로를 고정. |
| **URP Global Settings Hygiene** | GUID 스캔 기준 미해결 참조가 `Assets/Settings/UniversalRenderPipelineGlobalSettings.asset`에 49건 남아 있어, Unity 에디터에서 URP Global Settings 재생성/재할당 후 확정 커밋이 필요하다. |
