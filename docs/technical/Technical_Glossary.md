# 📖 Technical Glossary: Boss Raid Portfolio

이 문서는 프로젝트 내에서 통용되는 주요 용어와 개념을 정의합니다.

## 1. Character Entities

* **The Capsule (플레이어)**: 플레이어가 조작하는 캐릭터 객체. 현재 그래픽이 캡슐 형태이므로 내부적으로 'Player' 또는 'TheCapsule'로 지칭한다.
* **The Cube (보스)**: 레이드 대상인 AI 보스 객체. 정육면체 형태이며, 'Boss' 또는 'TheCube'로 지칭한다.
* **CameraRoot**: 플레이어 캐릭터 자식 객체로 존재하며, 카메라의 회전(Pitch, Yaw)의 중심축이 되는 Transform. 캐릭터의 몸통 회전과 독립적으로 동작한다.
* **BossVisual**: 보스(`The Cube`)의 애니메이션, UI, 이펙트 등 시각적 요소를 전담하는 컴포넌트. `BossController`의 로직과 분리되어 있다.

## 2. Input & Network

* **Input Packet (PlayerInputPacket)**: 매 프레임 발생하는 입력 데이터를 담은 구조체. `moveDir`, `lookYaw`, `lookPitch`, `buttons`를 포함한다.
* **Input Provider**: 입력을 생성하는 주체. `LocalInputProvider`(키보드/마우스)와 추후 구현될 `NetworkInputProvider`(RPC/데이터 패킷)로 나뉜다.
* **Bit-Packing (비트 패킹)**: 여러 개의 `bool` 버튼 상태를 1바이트(`byte`) 데이터로 묶어 네트워크 전송 효율을 극대화하는 기법.
* **Input Flag**: 비트 패킹 시 각 버튼의 자릿수를 지정하는 `Enum` (예: Dash, Attack).

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
* **CombatHUDController**: 전투 HUD 전용 컨트롤러. 플레이어/보스 HP 바, 이름 라벨, 고정형 데미지 텍스트를 한 컴포넌트에서 제어한다.
* **HUD 이름 라벨 정책**: `Text_PlayerHP`/`Text_BossHP` 슬롯을 체력 수치 대신 이름 라벨(`Player`, `Dragon`) 표시 용도로 사용하는 UI 정책.
* **HUD 부트스트랩 바인딩**: `PlayerController.InitializeCombatHUD()`에서 HUD 참조/보스 `Health`를 초기 탐색한 뒤 `Initialize`와 이름 라벨 세팅까지 한 번에 수행하는 시작 절차.
* **HP Fill 정규화 업데이트**: `HealthRatio`를 `Image.fillAmount`로 반영해 체력 UI를 갱신하는 방식. 수치 텍스트 갱신 없이도 이벤트 기반으로 즉시 동기화할 수 있다.
* **HUD 가시성 토글**: `CombatHUDController.ShowHud(bool)`로 플레이어/보스 체력 UI와 이름 라벨, 데미지 피드백 표시를 일괄 On/Off 하는 제어 패턴.
* **Title Scene**: 게임 시작 시점 전용 씬. `TitleSceneController`가 아무 키 입력을 감지해 전투 목적지(`GamePlay`)를 `SceneLoader`에 요청한다.
* **Loading Scene**: 씬 전환 중 비동기 로드를 담당하는 중간 씬. `SceneLoader`가 목적지 씬을 예약하고 `LoadingSceneController`가 진행률 UI와 활성화 타이밍을 제어한다.
* **Input Lock Duration (입력 잠금 시간)**: 타이틀 진입 직후의 잔존 키 입력으로 즉시 전환되는 문제를 막기 위한 짧은 대기 구간. 현재 `TitleSceneController`의 `_inputLockDuration`으로 제어한다.
* **SimultaneousDeathTest (동시 사망 테스트 컴포넌트)**: `GamePlayScene_TestResult`에서 동일 프레임 사망을 재현하기 위해, `K` 입력 시 플레이어/보스 `Health.TakeDamage`를 같은 프레임에 호출하는 테스트 스크립트(`Assets/Scripts/Test/SimultaneousDeathTest.cs`).
* **동시 사망 판정 우선순위 (GameManager)**: `GameManager.LateUpdate()`가 `_bossDead`를 먼저 검사해 결과를 확정하는 규칙. 플레이어와 보스가 동시에 사망하면 `Victory`를 반환한다.
* **Magic String (매직 스트링)**: 코드 내에 직접 하드코딩된 문자열 리터럴. 오타 위험이 크므로 `const` 상수로 관리해야 함.
* **Coroutine Cleanup**: `StopAllCoroutines()`를 호출하여 진행 중인 비동기 작업(무적 타이머 등)을 강제 중단하는 기법. 상태 전환(사망 등) 시 잔존 코루틴이 예상치 못한 부작용을 일으키는 것을 방지.
* **Generic State Machine (제네릭 상태 머신)**: `StateMachine<T>` 형태로 구현하여, 플레이어와 보스가 동일한 상태 관리 로직을 공유하면서도 각자의 상태 타입(`PlayerState` vs `BossState`)을 안전하게 사용할 수 있게 하는 기법.
* **Feature Toggle (기능 토글)**: 개발 중인 기능이나 특정 로직(예: 보스 추적, 회전)을 인스펙터 체크박스 하나로 켜고 끌 수 있게 하여, 테스트 효율을 높이고 버그 추적을 용이하게 하는 개발 패턴.
* **Priority Marker (우선순위 마커)**: 작업 목록의 중요도를 일관되게 표시하기 위한 표기 규칙. `🔴(1순위)`, `🟡(2순위)`, `🟢(3순위)` 순으로 관리한다.
* **Task Tag (작업 태그)**: 작업 대상/영역을 괄호로 명시하는 표기 방식. `(플레이어)`, `(보스)`, `(플레이어, UI)`처럼 단일 또는 복수 도메인으로 표기한다.
* **Planar Distance Gate (평면 거리 게이트)**: Boss의 상태 전환 거리 판정에서 높이(Y)를 제외하고 XZ 평면 거리만 사용해 점프/지형 높이 차로 인한 오판정을 줄이는 규칙.
* **Pattern Attack Range (패턴별 공격 사거리)**: `BossController`가 공격 패턴마다 별도 사거리(`Basic`, `Lunge`, `Projectile`, `AoE`)를 가지는 규칙. 공격 패턴 선택 시 현재 거리에서 유효한 패턴만 후보로 포함한다.
* **Range-Only Detection Trigger (거리 단일 감지 트리거)**: Idle/Searching에서 Combat(스크림 인트로) 진입을 감지 반경(`IsTargetInDetectionRange`)만으로 판정하는 규칙. 장애물/시야선(LOS) 여부와 무관하게 거리 조건만 충족하면 전투 전환이 발생한다.
* **Chase Hysteresis (추적 히스테리시스)**: 단일 공격 사거리 임계값 대신 현재 페이즈에서 활성화된 패턴의 `최대 사거리`(해제)와 `최대 사거리 + ChaseReengageBuffer`(재진입) 이중 임계값을 두어 Walk/Idle 경계 지터를 완화하는 기법.
* **Asset+Meta Pair Rule (에셋-메타 쌍 규칙)**: Unity 에셋은 파일만 커밋하면 참조가 보장되지 않는다. 참조 안정성을 위해 원본 에셋과 해당 `.meta`를 반드시 쌍으로 버전관리하는 규칙.
* **Dependency Closure Tracking (의존성 폐쇄 추적)**: 특정 씬/프리팹이 참조하는 직접/간접 에셋을 그래프 형태로 확장해 누락 없이 추적 세트를 산출하는 방식.
* **GUID Orphan Reference (GUID 고아 참조)**: YAML에 남아 있는 GUID가 로컬/레포 어디에도 존재하지 않아 `Missing`으로 해석되는 참조 상태.
* **Manual Import Baseline (수동 임포트 기준선)**: 저장소 용량 제약이 있을 때 대용량 서드파티 에셋은 Git에서 제외하고, 팀원이 동일 버전을 수동 임포트해 작업 기준선을 맞추는 운영 규칙.

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
* **Attack Window Result Event**: 공격 판정 시작(`EnableHitbox`)부터 종료(`DisableHitbox`)까지 누적된 결과를 1회 발행하는 이벤트. 현재 `DamageCaster.OnAttackWindowResolved(bool isHit, int totalDamage)`로 구현되어 HUD 피드백에 사용된다.
* **Fixed Damage Feedback (고정형 데미지 피드백)**: 월드 위치를 추적하지 않고 HUD의 고정 앵커에서 `HIT + 피해량`만 표시하는 피드백 방식. 현재는 적중 시 확대 후 짧은 페이드 아웃으로 마무리한다.
* **Ghost Hitbox Guard (잔존 히트박스 가드)**: 상태 전환/초기화 시 `ForceDisableHitbox()`와 `AttackState.Exit()`를 통해 공격 판정이 남지 않도록 강제 정리하는 보호 규칙.
* **Zero-Damage Filter (무데미지 필터)**: `DamageCaster.EnableHitbox`와 `Health.TakeDamage`에서 0 이하 데미지를 무시해 피격 이벤트/애니메이션 오작동을 방지하는 안전 장치.
* **Animation Event Bridge**: 애니메이터의 타임라인 이벤트를 코드 로직(`PlayerController` 등)으로 연결해주는 중계 클래스.
* **IBossAttackPattern**: 보스 공격 패턴 인터페이스 (Strategy Pattern 적용). `Enter`/`Update`/`Exit` 메서드를 정의하여 `BossAttackState`가 구체 패턴을 몰라도 실행할 수 있게 함.
* **BasicAttackPattern**: `IBossAttackPattern`의 기본 구현체. 보스의 근접 공격(애니메이션 재생 + DamageCaster 활성화 + 타이머 기반 종료)을 측술화.
* **Invincibility Frame (무적 시간)**: 피격 후 일정 시간 동안 추가 데미지를 받지 않는 보호 기간. `Health.SetInvincible(true/false)`와 코루틴으로 관리.
* **Bone-Synced Hitbox (본 동기화 피격 판정)**: `DamageCaster._castCenter`를 스켈레톤의 Bone 자식 Transform으로 설정하여, 애니메이션에 따라 히트박스 위치가 자동으로 동기화되는 기법. 코드 수정 없이 물리 판정과 애니메이션을 연동할 수 있음.
* **Partial Animation (부분 애니메이션)**: 애니메이션 클립 전체를 재생하지 않고, 특정 구간(예: 도약 부분)만 재생한 후 강제로 종료(`exitPhaseRatio`)하여 동작의 템포를 조절하는 기법. 복귀 모션 등을 생략하여 타격감을 높일 때 사용됨.

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

