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
    class BossAttackHitData { <<Struct>> }
    class BossAttackHitType { <<Enumeration>> }
    class BossAttackHitResolution { <<Enumeration>> }

    %% Core Components
    class PlayerController {
        <<MonoBehaviour>>
        +MoveSpeed float
        +Animator Animator
        -_stateMachine StateMachine
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
        +SetPlayerName(string)
        +SetBossName(string)
        +SetPlayerTorso(Sprite)
        +ShowHud(bool)
        +ShowDamageFeedback(bool, int)
    }
    class DamageCaster {
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
    ThirdPersonCameraController --> PlayerController : reads look cache / injects CameraRoot

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
        +LungeAttackSettings LungeAttackSettings
        +ProjectileAttackSettings ProjectileAttackSettings
        +BossProjectilePool ProjectilePool
        +Transform ProjectileSpawnPoint
        +SetLocomotionVisualSuppressed(bool)
        +GetPlanarDistanceToTarget() float
        +GetPlanarDistanceFromBasicAttackOriginToTarget() float
        +IsTargetInDetectionRange() bool
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
        -float _timer
        +Enter(BossController)
        +Update(BossController) bool
        +Exit(BossController)
    }

    class LungeAttackPattern {
        -LungeAttackSettings _settings
        +Enter(BossController)
        +Update(BossController) bool
        +Exit(BossController)
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
    BossController --> LungeAttackPattern : Owns
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
        +Update()
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
* **Boss Basic Range-Hitbox Sync Rule**: `basicAttackRange`와 `HeadDamageCaster.radius`를 동일 값으로 유지해 사거리 판정과 실제 타격 반경이 어긋나지 않도록 한다.
* **Boss Phase1 Attack Priority Rule**: Phase1에서 Basic/Lunge 조건이 동시에 만족되면 Basic을 우선 선택한다. Lunge는 Basic 범위를 벗어났고 Lunge 범위는 만족할 때만 선택한다.
* **Boss Lunge Root Motion Relay Rule**: Lunge 이동은 `rushPhaseRatio/MoveRaw` 수동 전진이 아니라, Animator `OnAnimatorMove`의 루트모션 델타를 `BossController.ApplyLungeRootMotion(deltaPosition, normalizedTime)`으로 전달해 부모 루트를 이동시킨다. 활성화 구간은 `SetLungeRootMotionEnabled(true/false)`로 제한하며, 릴레이는 시작 시 `Visual` 로컬 기준 포즈를 캐시하고 `OnAnimatorMove`/종료 시 복원해 부모 루트와 자식 비주얼 좌표 분리를 방지한다. `ResolveAppliedDeltaPosition` 기본 경로는 XZ Animator 델타를 사용하고, 미소 프레임에서는 Visual 월드 델타 폴백을 사용한다.
* **Boss Lunge Motion Distribution Tuning Rule**: 실험 9에서는 Lunge `normalizedTime` 구간별 이동량 배분 보정을 위해 `LungeAttackSettings`의 `midBoost*`/`lateReduce*` 파라미터를 적용한다. `OnValidate`에서 시작/종료 구간은 `0~1`로 clamp하고, 종료값이 시작값보다 작으면 시작값으로 보정하며, 배수(`midBoostScale`, `lateReduceScale`)는 `0` 이상으로 강제한다.
* **Boss Lunge Fixed Timing Rule**: Lunge 판정/종료 타이밍은 현재 `LungeAttackPattern`의 고정 비율(`HitboxOff 0.8`, `Exit 1.0`)을 사용한다. Enter 순서는 `SetLungeRootMotionEnabled(true)` 이후 `PlayLungeAttack()` 호출이다.
* **Boss Attack2 Phase Marker Rule**: Attack2의 구간 분리(`Windup -> PreLaunch -> Launch -> Airborne -> Land`)는 애니메이션 이벤트(예: `PreLaunchStart`, `Launch`, `Land`)를 우선 기준으로 사용한다. 이벤트는 단일 슬롯이 아닌 큐(Queue)로 누적/소비해 프레임 드랍 시 마커 유실을 방지한다. 이벤트 누락 시에는 `normalizedTime` 임계값 폴백으로 동일 전이를 보장하고, 누락 마커는 `MarkerPathWarn` 로그로 추적한다.
* **Boss Attack2 Launch Guard Rule**: Attack2의 `launchNormalizedTime`/`landSnapNormalizedTime`은 종료 시점(`1.0`) 이전 상한(`0.98`)으로 강제한다. 설정값이 상한을 넘으면 `OnValidate`에서 자동 보정해 Launch 영구 미진입 구성을 방지한다. Relay는 AnimEvent 누락 시 `AnimEventSynth`로 동일 마커를 합성 큐잉한다.
* **Boss Attack2 Landing Snap Rule**: `Windup/PreLaunch` 동안에는 `stepOffset`을 `0`으로 낮추고, 루트모션 Y를 차단한 뒤 `RaycastNonAlloc` 기반 Ground 스냅으로 목표 높이를 유지한다. `Launch` 프레임에서 ground lock을 해제하고 `stepOffset`을 원복한 뒤 루트모션 Y를 허용한다. `Land` 이벤트를 우선 기준으로 1회 스냅을 적용하고, 이벤트 누락 시에는 `landSnapNormalizedTime` 폴백으로 동일 스냅 경로를 보장한다.
* **Attack2 Spatial Probe Rule**: `[Attack2Landing][SpatialProbe]` 로그는 `player`, `boss`, `visual`, `red(Boss/Visual/Red)` 좌표와 상대 벡터(`player-boss`, `player-red`, `boss-red`, `visualInBoss`, `redInBoss`) 및 `redPath`를 함께 출력한다. GroundLock 시작/해제, 착지 스냅, 모션 종료와 RootMotion 로그에 결합해 좌표계 이탈 원인을 프레임 단위로 추적한다.
* **Attack2 Inspector Timeline Rule**: `BossControllerEditor`는 Attack2 설정을 핵심(타이밍 3종)과 고급(지면 레이/스냅)으로 분리해 노출한다. 핵심 타이밍은 `normalizedTime` 타임라인 바(`PreLaunch`, `Launch`, `Land`, `Guard 0.98`)로 즉시 시각화해 수치 변경 영향 범위를 인스펙터에서 바로 확인할 수 있어야 한다.
* **Attack2 Gizmo Feature Toggle Rule**: `BossController`의 `OnDrawGizmosSelected`는 단일 고정 그리기 대신 기능별 토글(`showAttackRangesGizmo`, `showDetectionRangeGizmo`, `showAttack2GroundProbeGizmo`, `showAttack2SnapWindowGizmo`, `showAttack2SpatialLineGizmo`)로 분리한다. Attack2 디버깅 시 필요한 기즈모만 선택적으로 표시해 시야 오염과 튜닝 혼선을 줄인다.
* **Attack2 Recovery Priority Rule**: Attack2 개선에서 디버그 계층(인스펙터/기즈모/로그)을 추가했음에도 핵심 증상(타겟 근접 도약 실패, 원위치 당김)이 유지되면, 추가 튜닝을 누적하지 않고 마지막 안정 커밋 기준으로 이동 경로를 먼저 회귀한다.
* **Boss Detection Trigger Rule**: Idle/Searching에서 Combat 전환(스크림 인트로 진입)은 `IsTargetInDetectionRange()` 기준으로 즉시 수행한다. 현재 보스 감지는 장애물/시야선(LOS) 판정을 사용하지 않는다.
* **Boss Chase Hysteresis**: 단일 임계값 대신 "현재 페이즈에서 활성화된 패턴 중 최대 사거리"(해제)와 `최대 사거리 + ChaseReengageBuffer`(재진입) 이중 임계값을 사용해 경계 왕복 지터를 완화한다.
* **Player Stun Hit Classification Rule**: 플레이어 피격은 `BossAttackHitType` 메타데이터로 분기한다. `Attack1`은 일반 피격(데미지+Hit), `Attack2`는 스턴 전용(무데미지), `Attack3/Attack4 Projectile`은 `Projectile Count Timer` 내 1타 일반 피격/2타 스턴으로 처리한다.
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

## 4. Implementation Status Check

### 4.1. Core Systems & Input
| Component | Status | Note |
| --- | --- | --- |
| **IInputProvider** | ✅ Done | `LocalInputProvider.cs` 구현 완료. |
| **Input Packet** | ✅ Done | `PlayerInputPacket` (Bit-packing) 적용 완료. |
| **StateMachine** | ✅ Done | `BossRaid.Patterns` 네임스페이스 적용 및 구현 완료. |
| **Physics System** | ✅ Done | `NonAlloc` 물리 판정(OverlapSphere) 및 최적화 완료. |
| **Object Pooling** | ✅ Done | `BossProjectilePool` 기반 투사체 재사용(Prewarm/Max/Expand) 구현 완료. |
| **Third-Person Camera Module** | ✅ Done | `Assets/Scripts/Camera/ThirdPersonCameraController.cs`를 메인 카메라 측 모듈로 분리해 카메라 설정을 카메라 오브젝트로 이동했다. |
| **Package Baseline** | ✅ Done | Unity 2022.3 기준으로 package manifest 정리 및 lock 재생성 경로 복구 (`URP/VFX 14.0.12`, `TMP 추가`, Unity 6 전용 의존성 제거). |
| **External Asset Distribution Policy** | ✅ Done | 런타임 실사용 에셋만 Git/LFS로 선별 추적한다. 기준은 GUID 의존성 폐쇄(씬/프리팹/설정의 직접+간접 참조)이며, `Assets/TextMesh Pro` 루트와 미사용 서드파티 리소스는 제외한다. Unity 참조 안정성을 위해 에셋과 `.meta`를 쌍으로 버전관리한다. |

### 4.2. Player System
| Component | Status | Note |
| --- | --- | --- |
| **Movement Logic** | ✅ Done | `MoveState`로 로직 이관 완료. |
| **Dash Logic** | ✅ Done | Cooldown 및 Edge-triggering 기능 포함 구현 완료. |
| **Jump Logic** | ✅ Done | `JumpState` 구현 완료. 현재 게임 디자인 기준 점프 입력 전환은 비활성(주석/F10 유지) 상태이며 필요 시 재활성 가능. |
| **Camera Logic** | ✅ Done | `ThirdPersonCameraController`가 메인 카메라에서 `lookYaw/lookPitch`를 1차 입력으로 처리하고, `CameraRoot`를 관리한다. smoothing/auto-behind 데이터는 유지하되 현재 인스펙터 노출은 숨김 처리되어 있다. |
| **Attack Logic** | ✅ Done | `AttackState` 구현 완료. 콤보/캔슬/개별 데미지 지원. 상태 전환 시 `AttackState.Exit()`에서 히트박스를 강제 종료해 잔존 판정을 방지하며, `DamageCaster`는 0 데미지 윈도우를 무시한다. |
| **Hit/Damage System** | ✅ Done | `IDamageable`, `DamageCaster`, `Health` + `IBossAttackHitReceiver` 기반 보스 공격 메타데이터 라우팅 구현 완료. 플레이어는 `StunState`/`Projectile Count Timer`/후속 무적 규칙을 사용하고, 보스는 공격 준비/실행 중 피격 모션을 무시한다. 플레이어/보스의 점멸은 공용 `BlinkWhiteEffect` 컴포넌트(`Assets/Scripts/Common/Visual/BlinkWhiteEffect.cs`)가 담당하며 `_BlinkWhite` 셰이더(`Assets/Shaders/BlinkWhiteLit.shader`)를 `MaterialPropertyBlock`으로 제어한다. |
| **Asset Integration** | ✅ Done | `PlayerAnimator`의 `Hit/Attack1/2/3/Die` 상태 모션 재연결 완료(2026-02-21). |
| **Environment Fix Guard (환경 오류 복구)** | ✅ Done | `Assets/Editor/PlayerAnimatorGuard.cs`로 환경 변경 시 발생하는 Animator 참조 오류를 자동 복구/검증한다. 필수 state/motion + 파라미터(`Speed` Float, `Hit` Trigger) 누락 점검, 모든 Layer + 중첩 StateMachine 재귀 순회, Locomotion BlendTree 자식 모션 검증, 중복 상태명 경고, 로드/임포트/이동/메뉴 경로를 지원하며 `Hit` 상태명은 `PlayerController.ANIM_STATE_HIT` 상수를 공용 참조한다. 추가로 `Attack1/2/3` 클립의 `OnHitStart/OnHitEnd` 이벤트 자동 보정 및 누락/순서 검증, `Tools/Validation/Fix Player Attack Events` 메뉴를 포함한다. |

### 4.3. Boss System (The Dragon)
| Component | Status | Note |
| --- | --- | --- |
| **Boss Logic (FSM)** | ✅ Done | `BossController` 상태 머신 (Idle, Combat, Searching, Dead) |
| **Boss Sensors** | ✅ Done | `IsTargetInDetectionRange`(XZ 거리 기반) 단일 규칙으로 Idle/Searching 전투 진입을 처리한다. 장애물 LOS 센서는 제거됨 |
| **Boss Navigation** | ✅ Done | `MoveTo` (추적 이동) 및 `RotateTowards` (회전) 로직 + AoE 공중 연출 중 Locomotion 시각 잠금 가드 + `ChaseReengageBuffer` 기반 히스테리시스 추적 |
| **Boss Visuals** | ✅ Done | 구조 분리 및 Dragon Asset(Animator/BlendTree) 통합 완료. `PlayFlyForward` 폴백을 비행 계열로 정리해 Walk 혼입 방지. |
| **Boss Combat** | 🔃 progress | `Pattern 1`(Basic), `Pattern 2`(Lunge), `Pattern 3`(Projectile: Flame Attack + Homing + Vertical Follow + VFX create/hit + hitReturnDelay + postFireRecovery/exitNormalizedTime) 완료. 패턴별 공격 사거리 분리(`Basic/Lunge/Projectile/AoE`) 및 거리 기반 패턴 후보 필터링, 최대 사거리 기반 추적 히스테리시스 반영. Basic 사거리 기준점은 `basicAttackRangeOrigin`(기본 씬: `HeadDamageCasterPlace`)으로 분리되었고, `basicAttackRange`-`HeadDamageCaster.radius` 동기화로 판정 반경을 일치시켰다. Lunge는 루트모션 브리지(`OnAnimatorMove -> ApplyLungeRootMotion`) + 시작 방향 고정 경로를 사용한다. 2026-03-04 기준으로 실험 9 이동량 분포 보정(`midBoost/lateReduce` 구간 배수)과 `scale` 로그 추적이 추가되었으며, 판정/종료 타이밍은 고정 비율(`0.8/1.0`)을 유지한다. Phase1에서는 Basic/Lunge 동시 충족 시 Basic 우선 규칙을 적용한다. `Pattern 4`(AoE)는 fully red active window 반경 판정 + circle당 target 1회 타격 규칙으로 진행 중이다(늦게 진입한 target 포함, invul ignore는 소비하지 않음). |
| **Attack2 PreLaunch Ground Lock** | 🔃 progress | `LungeAttackPattern` 구간 분기(`Windup/PreLaunch/Launch/Airborne/Land`) 및 `BossController` ground lock/stepOffset 제어, 마커 큐 누적/소비(`PreLaunchStart/Launch/Land`) + `normalizedTime` 폴백까지 코드 반영 완료. `launch/land` 정규화 시점은 `0.98` 상한으로 강제해 종료 전 Launch 미진입 구성을 차단하고, Relay의 `AnimEventSynth`로 이벤트 누락 시 동일 마커를 합성한다. 루트모션 적용은 Animator `deltaPosition` 기반 단일 추종을 유지한다. `[Attack2Landing]` 로그는 `MarkerPathWarn`, `GroundSnapMiss`, `GroundSnapSkipMaxDistance`, `RootMotionMove/RootMotionRelayProbe`, `SpatialProbe(player/boss/visual/red 좌표)`를 포함해 실패 원인/좌표 경로를 추적한다. Unity 실플레이 회귀 검증(머리 탑승/발 꺼짐/착지 정렬)은 남아 있다. |
| **Attack2 Inspector & Gizmo UX** | ⚠ review | 인스펙터/기즈모 가시성 개선은 완료됐지만, 핵심 이동 증상(플레이어 근접 도약 실패, 원위치 당김)은 해결하지 못했다. 다음 단계에서 이동 로직을 마지막 안정 커밋 기준으로 회귀한 뒤 필요 최소 UI/디버그만 재적용한다. |
| **Attack2 Repro Harness (Test Scene)** | ✅ Done | `GamePlayScene_TestResult` 로드 시 `BossAttack2ReproHarness`를 자동 생성해 플레이어를 보스 전방 재현 위치로 고정한다. 수동 착지지점 배치 없이 Attack2 위로 올라감 회귀를 반복 재현할 수 있다. |

### 4.4. User Interface (UI)
| Component | Status | Note |
| --- | --- | --- |
| **UI System** | ✅ Done | 전투 HUD 배치 + `CombatHUDController` 연동 완료. `Health.OnDamageTaken/OnDeath` 이벤트로 플레이어/보스 HP Fill을 즉시 갱신하고, `DamageCaster.OnAttackWindowResolved` 결과를 `HIT + 피해량` 고정형 피드백(스케일 강조 후 짧은 페이드 아웃)으로 표시한다. 이름 라벨(`Player`, `Dragon`) 및 `ShowHud(bool)` 기반 전체 표시 제어를 포함한다. |

### 4.5. Game Logic & Flow
| Component | Status | Note |
| --- | --- | --- |
| **Game Loop** |  ✅ Done | `TitleSceneController`로 아무 키 입력 시작점을 분리했고, `SceneLoader` + `LoadingSceneController` 경유 전투 진입/`GameManager` 결과 처리까지 연결했다. `GamePlayScene_TestResult` + `SimultaneousDeathTest`로 동시 사망 시 `Victory` 출력 검증을 완료했다. 일반 실플레이 회귀 시나리오 검증은 남아 있다. |

### 4.6. Network Architecture
| Component | Status | Note |
| --- | --- | --- |
| **Netcode Prep** | ⬜ Todo | 추후 `NetworkInputProvider` 추가 예정. |

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
| Component | Status | Note |
| --- | --- | --- |
| **AoE Heading Sampling** | ✅ Done | AoEAttackPattern의 타겟 속도 샘플링은 Unity 2022 기준 Rigidbody.velocity를 사용한다. |
| **Editor Assembly Anchor** | ✅ Done | Assets/Editor/EditorAssemblyAnchor.cs를 통해 에디터 전용 어셈블리 생성 경로를 고정. |
| **URP Global Settings Hygiene** | 🔃 progress | GUID 스캔 기준 미해결 참조가 `Assets/Settings/UniversalRenderPipelineGlobalSettings.asset`에 49건 남아 있어, Unity 에디터에서 URP Global Settings 재생성/재할당 후 확정 커밋이 필요하다. |
