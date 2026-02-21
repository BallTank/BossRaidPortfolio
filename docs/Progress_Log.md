# 🚀 Progress Log: Boss Raid Portfolio

## 📅 [2월 1주차] 목표: 플레이어 컨트롤러 및 상태 머신

### **2026-02-21 (토: Player Animator 모션 복구 및 누락 가드 추가)**

* **오늘 반영한 작업**
* `PlayerAnimator.controller`에서 `Hit`, `Attack1`, `Attack2`, `Attack3`, `Die` 상태의 깨진 Motion GUID를 현재 프로젝트의 `CombatGirlsCharacterPack` 클립으로 재연결.
* `MoveState`의 점프 전환은 게임 디자인 의도대로 다시 비활성 상태(주석 유지)로 복구.
* `Assets/Editor/PlayerAnimatorGuard.cs`를 추가해 필수 상태/모션 누락을 자동 검증.
  * 에디터 리로드 시 자동 점검.
  * `PlayerAnimator.controller` 재임포트 시 자동 점검.
  * `Tools/Validation/Validate Player Animator` 수동 점검 메뉴 제공.
* `PlayerAnimatorGuard` 리팩터링:
  * 모든 Animator Layer + 중첩 `AnimatorStateMachine`까지 재귀 순회해 필수 상태를 탐색하도록 개선.
  * 필수 파라미터 계약(`Speed: Float`, `Hit: Trigger`) 검증 추가.
  * 동일 상태명 중복 시 경고 로그를 출력해 구성 오류를 조기 감지.
  * `PlayerAnimator.controller`의 이동 경로(`movedAssets`)까지 후처리 감지 범위 확장.
  * `PlayerController.ANIM_STATE_HIT` 상수를 복구하고, 에디터 가드도 동일 상수를 참조하도록 정리.

* **기술적 고려**
* 상태 이름은 남아 있어도 Motion GUID가 유실되면 런타임에서 즉시 동작하지 않으므로, “상태 존재 여부 + 모션 바인딩 유효성”을 함께 검증해야 함.
* 재발 방지를 위해 수동 점검에 의존하지 않고, 에디터 이벤트(로드/임포트) 기반 자동 가드를 기본 안전망으로 설정.
* Animator 누락 문제는 Top-Level 상태만 검사하면 놓칠 수 있으므로, 서브 상태 머신까지 재귀적으로 검사해야 재발 방지 효과가 유지됨.

### **2026-02-20 (금: 애니메이션 패턴 전환 우선순위 정리)**

* **오늘 반영한 작업**
* `BossCombatState.Update()`의 우선순위를 조정해, 추적 구간에서 공격 가능 조건을 만족하면 `MoveTo`/`PlayMove`보다 `AttackState` 전환을 먼저 수행.
* AoE/공중 연출 패턴 진입 시 Walk 애니메이션과 충돌하지 않도록 전환 직전 이동 호출 경로를 제거.
* `Walk + TakeOff` 혼입 이슈 해결을 위한 1차 패치를 반영.

### **2026-02-20 (금: AoE 추적 중 Walk/TakeOff 애니메이션 충돌 수정)**

* **오늘 완료한 작업**
* `BossController`에 `SetLocomotionVisualSuppressed(bool)`를 추가해 공중 연출 중 `MoveTo`/`StopMoving`에서 `PlayMove`/`PlayIdle`가 덮어쓰지 않도록 시각 잠금을 적용.
* `AoEAttackPattern.Enter/Exit`에서 잠금을 On/Off 하도록 연결해 `TakeOff -> FlyForward -> FlyIdle -> Land` 체인 동안 Walk 혼입을 차단.
* `BossVisual.PlayFlyForward()` 폴백을 `PlayMove`에서 `PlayFlyIdle`로 변경해 FlyForward 상태가 없는 Animator에서도 공중 연출이 깨지지 않도록 보정.

* **기술적 고려**
* FSM 전환 우선순위만으로는 프레임 경계에서 `MoveTo`/`StopMoving` 호출이 시각 상태를 오염시킬 수 있어, 공격 패턴 단위의 시각 잠금 계층을 추가.
* AoE 패턴 종료 또는 인터럽트 시 `Exit`에서 잠금을 해제해 Combat/Search/Idle 복귀 후 Locomotion 재생이 정상 동작하도록 보장.

### **2026-02-20 (금: Boss 추적/Flame 복귀 지터 완화 2차 패치)**

* **✅ 오늘 완료한 작업**
* `BossCombatState`의 거리 판정을 `Vector3.Distance`에서 수평(XZ) 기준 `GetPlanarDistanceToTarget()`로 전환.
* `BossController`에 `chaseReengageBuffer`를 추가하고, `BossCombatState`에 추적 래치(히스테리시스)를 적용해 `Speed 3.5 ↔ 0` 왕복을 완화.
* `BossIdleState`, `BossSearchingState`, `BossHitState`의 거리 판정도 동일한 평면 기준으로 통일.
* `ProjectileAttackPattern`에 `postFireRecoveryDuration`, `exitNormalizedTime`을 도입해 Flame Attack 종료 후 조기 Locomotion 복귀를 방지.

* **🧠 기술적 고민**
* 단일 임계값 기반 전환은 경계 근처에서 상태 왕복이 발생하기 쉬워 애니메이션 파라미터 튐을 유발하므로, 이중 임계값(히스테리시스)으로 완화.
* 발사 수(`volleyCount`)만으로 패턴 종료를 판단하면 클립 잔여 구간이 잘릴 수 있어 최소 대기 + `normalizedTime` 조건을 결합.

### **2026-02-20 (금: 문서 한글 인코딩 가드레일 정립)**

* **✅ 오늘 완료한 작업**
* `AI_Maintenance_Guide.md`에 UTF-8 저장/검증/복구 절차를 추가해 문서 유지보수 프로토콜에 반영.
* `Coding_Standard.md`에 문서 인코딩 표준 섹션을 추가하고 PowerShell `-Encoding utf8` 명시 규칙을 고정.
* 한글 핵심 문서(`Input_FSM_Flow.md`, `System_Blueprint.md`, `Progress_Log.md`) 편집 전후 깨짐 패턴 점검 기준을 체크리스트에 반영.

* **🧠 기술적 고민**
* 터미널 출력 깨짐과 파일 손상을 구분하지 않으면 불필요한 재저장으로 손상이 확대될 수 있어, "출력 확인 -> 파일 인코딩 확인 -> 복구" 순서를 표준화.
* 이미 손상된 텍스트는 인코딩 전환만으로 복구되지 않으므로, 정상 커밋 기준 재적용 원칙을 명시.

### **2026-02-20 (금: 문서 최신화 동기화)**

* **✅ 오늘 완료한 작업**
* `System_Blueprint.md`에 `AoEAttackPattern` 전략 클래스 및 `BossController` 소유 관계를 다이어그램/관련 코드 목록에 반영.
* `Animator_Setup_Guide.md`에 `PlayFlyForward`의 비행 폴백(`PlayFlyIdle`)과 AoE 구간 `SetLocomotionVisualSuppressed` 운용 규칙을 추가.
* `Animation_Implementation_Log.md`, `Input_FSM_Flow.md`, `Technical_Glossary.md`에 Walk-Flight 충돌 방지 규칙(시각 잠금/폴백)과 동작 맥락을 동기화.

* **🧠 기술적 고민**
* 애니메이션 문제는 코드 수정만으로 끝나지 않고 Animator 설정 가이드/흐름 문서/용어집이 함께 갱신되어야 재발 방지 효과가 생긴다.
* 시각 잠금과 폴백은 런타임 예외 경로에서만 드러나므로, 문서에 명시해 테스트 케이스(상태 미존재, 경계 구간 지터)까지 함께 공유하는 것이 중요하다.

### **2026-02-02 (월): 기획 및 아키텍처 설계**

* **작업 내용**
* GDD(Technical Architecture) 초안 작성 및 시스템 로드맵 수립.
* 입력 시스템 인터페이스(`IInputProvider`) 및 데이터 구조(`PlayerInputPacket`) 의사코드 설계.


* **기술적 포인트 (Senior's Review)**
* **Decoupling**: 입력과 로직을 분리하여 나중에 `NetworkInputProvider`만 갈아 끼우면 멀티플레이어가 가능하도록 설계함.
* **Data-Oriented**: 싱글플레이 단계부터 데이터를 구조체(`struct`)화하여 메모리 효율과 네트워크 직렬화 편의성을 고려함.



---

### **2026-02-03 (화): 입력 시스템 및 코어 모터 구현**

* **작업 내용**
* Unity New Input System 연동 및 `PlayerControlInput` 바인딩 완료.
* `PlayerInputPacket` 내 **Bit-masking** 적용 (1바이트로 여러 버튼 상태 패킹).
* **Camera-Relative Movement** 구현: 카메라가 바라보는 방향 기준 이동 벡터 연산 완료.
* Character Body와 CameraRoot의 회전 로직 분리.


* **기술적 포인트 (Senior's Review)**
* **Bitwise Operation**: `bool` 변수 나열 대신 비트 연산(`&`, `|`, `~`)을 사용하여 패킷 크기를 최소화함. 면접 시 "네트워크 대역폭 최적화 경험"으로 어필 가능.
* **Vector Math**: `cameraRoot.forward`에서 `y`값을 제거하고 정규화(`Normalize`)하여, 경사로에서도 일정한 이동 속도를 유지하는 조작감 구현.



---

### **2026-02-04 (수): FSM Refactoring & Dash Implementation**

* **작업 내용**
    * `StateMachine` 및 `BaseState` 클래스 구현 (Generic Architecture).
    * `PlayerController`로부터 이동/회전 로직을 `MoveState`로 완전 이관.
    * `DashState` 및 **대시 강화(Cooldown, Edge-trigger)** 구현.
    * `JumpState` 구현: **Air Control(공중 제어)** 및 중력 적용, 착지 판정.
    * `AttackState` 구현: **3단 콤보 시스템**, 선입력(Buffer), 대시 캔슬(Animation Cancel).
    * 인스펙터에서 조절 가능한 대시 설정(`Duration`, `Multiplier`, `Cooldown`) 추가.
    * `PlayerController`는 상태 관리자 및 데이터 컨텍스트 역할 수행.

* **기술적 포인트**
    * **Separation of Concerns**: 상위 컨트롤러는 설정과 컨텍스트만 관리하고, 실제 로직 수행은 State 클래스에 위임하여 SRP(단일 책임 원칙)를 준수함.
    * **Input Edge-Triggering**: 대시 버튼을 꾹 누를 때 발생하는 연속 대시 문제를 `bool` 플래그를 이용한 엣지 트리거 방식으로 해결하여 의도치 않은 행위를 방지함.
    * **Combo Input Buffer**: 공격 애니메이션 도중 입력을 미리 받아두는 `_reserveNextCombo` 로직을 통해 끊김 없는(Fluid) 콤보 연계를 구현함.
    * **Animation Cancel**: 공격 후딜레이(Release Time) 구간에서 대시로 캔슬 가능하도록 `CheckDashCancel`을 구현하여 조작 반응성을 높임.
    * **Serialized Configuration**: 기획자가 코드 수정 없이 로직 밸런스를 조절할 수 있도록 `[SerializeField]`를 적극 활용함.

* **기술적 고민**
    * **State Transition**: 현재 `MoveState` 내부에서 `input.HasFlag(Dash)`를 체크하여 전환하고 있음. 상태가 많아지면 전환 로직이 복잡해질 수 있는데, `TransitionTable` 등을 도입할지 고민 필요.
    * **Project Structure**: 파일이 점차 늘어남에 따라 폴더를 기능 단위(`Player`, `Patterns`, `UI` 등)로 분리하고 네임스페이스를 더 세분화할 것인지 검토 중.



---

### **2026-02-05 (목): Codebase Analysis & Documentation**

* **작업 내용**
    * **Dependency Analysis**: 코드 의존성 분석 (Strong vs Weak Coupling) 및 다이어그램 시각화.
    * **Documentation Update**: `Dependency_Analysis.md` 작성.
    * **Animation Refactoring**: 애니메이션 상태 이름을 문자열 상수(`const`)로 변환하여 코드 안정성 및 유지보수성 확보.
    * **Interview Prep**: 기술 면접 대비 코드 구조 및 설계 의도 정리 (Codebase Analysis).
    * **FSM Visualization**: 상속 및 인터페이스 구조를 `Mermaid` 클래스 다이어그램으로 시각화.

* **기술적 포인트**
    * **Coupling & Cohesion**: `PlayerController`와 `State` 클래스 간의 결합도를 분석하고, 의존성 주입(Dependency Injection)과 인터페이스(`IInputProvider`) 사용의 중요성을 재확인함.
    * **Documentation as Code**: 문서를 단순 텍스트가 아닌, 엔지니어링 산출물로 취급하여 코드 변경 사항(상수명 등)을 즉시 반영함.
    * **Visual Communication**: 복잡한 FSM 구조를 다이어그램으로 표현하여 비주얼 디버깅 및 협업 효율을 높임.

* **기술적 고민**
    * **Magic Strings**: 애니메이션 상태명 등을 문자열 리터럴로 사용할 때의 위험성(오타, 유지보수)을 인지하고, `const` 상수로 관리하는 패턴을 문서화 및 코드에 적용할 필요성 느낌.
    * **Documentation Debt**: 기능 구현에 집중하다 보면 문서가 낙후되는 문제가 있음. 이를 방지하기 위해 '작업 후 즉시 문서화' 파이프라인(AI Maintenance Guide)을 철저히 준수하기로 함.

* **Next Plan (2026-02-07)**
    * **Combat Testing**: 실제 공격 판정(Hitbox)과 이펙트 연동.
    * **Combo Logic**: 선입력(Buffer)과 애니메이션 캔슬 타이밍 미세 조정.

---

### **2026-02-06 (금): Hitbox 및 피격 시스템 구현**

* **작업 내용**
    * **Hitbox System**: 
        * `DamageCaster`: `Physics.OverlapSphereNonAlloc`을 사용한 최적화된 충돌 감지 로직 구현.
        * `PlayerAnimationEvents`: 애니메이터 이벤트(`OnHitStart`, `OnHitEnd`)를 수신하여 `PlayerController`로 중계하는 브리지 역할 수행.
        * **Animation Event Keying**: 공격 애니메이션 클립 복제 및 프레임 단위 이벤트 삽입 완료.
    * **Damage Architecture**: 
        * `IDamageable`: 타겟 종류에 관계없이 데미지를 전달할 수 있는 공통 인터페이스 정의.
        * `Health`: `IDamageable`을 구현하여 HP 관리, 사망 처리, 피격 이벤트(`Action<int>`)를 제공하는 컴포넌트 구현.
    * **Documentation & Blog**: 
        * `docs/blog/Hitbox_and_StateFlow_Analysis.md`: 기술 블로그 포스트 작성.
        * `walkthrough.md`: 전체 시스템 흐름 및 실행 파이프라인 심층 분석 문서 작성.

* **기술적 포인트 (Senior's Review)**
    * **Garbage-Free Physics**: `OverlapSphereNonAlloc`을 통해 런타임 가비지 생성을 방지함. 프레임당 수십 번 호출되어도 GC 스파이크가 발생하지 않는 안정적인 액션 엔진 기반 마련.
    * **Synchronous State Logic**: `ChangeState` 호출 시 상태 변수는 즉시 교체되지만, 로직 실행 루프는 다음 프레임부터 반영되는 동기적 흐름을 정확히 통제함.
    * **Interface-Driven Design**: 인터페이스 사용으로 공격자(`DamageCaster`)와 피격 대상(`Health`) 간의 결합도를 최소화함.

* **Troubleshooting (Solved)**
    * **Issue**: `EnableHitbox` 이벤트가 수신되나 실제 로그가 출력되지 않음.
    * **Root Cause**: `PlayerController` 인스펙터 상에서 `_damageCaster` 필드 할당 누락 확인.
    * **Solution**: 인스펙터 연결 및 물리 레이어(`Enemy`) 설정 검증을 통해 해결.


---
---

### **2026-02-09 (월): Player Dead State & Boss Pattern 1**

* **작업 내용**
    * **Player Dead Logic (Prioritized)**:
        * Boss FSM 구현 전, 플레이어와 보스 공통으로 사용될 `DeadState` 및 사망 처리 로직 선행 구현 결정.
        * `Health.OnDie` 이벤트와 애니메이션 연동, 입력 차단(Input Block) 로직 설계.
    * **Boss AI 구조 설계**: `BossController`를 통해 거리 기반의 상태(Idle, Combat, Searching) 관리.
    * **Refactoring**: `BossController`의 비주얼 로직(애니메이션, UI)을 `BossVisual` 클래스로 분리하여 SRP(단일 책임 원칙) 준수.
    * **Documentation**: `Input_FSM_Flow.md`에 사망 상태(Dead State) 흐름 추가 및 `Boss_Algorithm_Design.md` 작성.

* **기술적 포인트**
    *   **Event-Driven Death**: 매 프레임 체력을 체크하는 것이 아니라, `OnDie` 이벤트를 구독(Subscribe)하여 상태 전환 비용을 최소화.
    *   **State Reusability**: `DeadState`를 플레이어와 보스가 공유하거나 유사한 로직으로 처리하여 코드 중복 방지.
    *   **Raycast Optimization**: `CheckLineOfSight`에서 `~LayerMask`와 눈높이(Offset)를 사용하여 연산 효율과 정확도 확보.
    *   **Visual Debugging**: `OnDrawGizmos`를 활용해 감지 범위와 시야각을 에디터에서 직관적으로 확인 가능하도록 구현.
    *   **Decoupling (Visual)**: `BossController`가 `Animator`를 직접 제어하지 않고 `BossVisual` 컴포넌트에 위임하여, 로직 변경 시 비주얼 스크립트 수정 최소화.

---

### **2026-02-09 (월): FSM Generic Refactoring & Null-Safe 패턴**

*   **작업 내용**
    *   **StateMachine 제네릭화**: Player/Boss 전용 `StateMachine`을 `StateMachine<TState>` 하나로 통합.
    *   **BaseState 제네릭화**: `BaseState<TController>`로 변경하여 Player/Boss 모두 재사용 가능하도록 리팩토링.
    *   **IState 인터페이스 도입**: `Enter()`, `Exit()` 공용 계약 정의.
    *   **PlayerVisual 통합**: `PlayerAnimationEvents.cs` 기능을 `PlayerVisual.cs`로 병합 후 삭제.
    *   **비트 연산 분석**: `((1 << layer) & obstacleMask) != 0` 코드의 LayerMask 동작 원리 학습 및 문서화.
    *   **Null-Safe 패턴 적용**: `BossVisual`이 미할당 시 NullReferenceException 방지를 위해 `?.` 연산자 적용.
    *   **SearchingState 속도 분리**: 탐지 범위 벗어나도 느린 속도(`searchingMoveSpeed`)로 추적 유지.
    *   **기술 블로그 작성**: `0209_Bitwise_and_NullSafe_Pattern.md` 작성.

*   **🧠 기술적 고민 (Q&A 학습 기록)**

    | 질문 | 결론 |
    |------|------|
    | **비제네릭 vs 제네릭 BaseState 차이?** | 비제네릭은 `PlayerController` 하드코딩 → Boss 사용 불가. 제네릭 `BaseState<TController>`는 타입 파라미터로 유동적 → 재사용성 확보. |
    | **PlayerBaseState와 BossBaseState 통합 가능?** | Update 시그니처가 다름 (`PlayerInputPacket` vs 없음). `NoInput` 타입으로 강제 통합 시 KISS 위반 및 의도 모호화. **분리 유지가 Clean Code**. |
    | **`TController`에서 T의 의미?** | **Type**의 약자. C# 제네릭 네이밍 관례로 `T` + 역할명(예: `TState`, `TInput`)으로 명명. |
    | **`1 << layer`의 의미?** | 레이어 인덱스(0~31)를 32비트 비트마스크로 변환. 예: layer=8 → 256 (0001 0000 0000). |
    | **AND 연산으로 뭘 확인?** | 충돌체 레이어가 `obstacleMask`에 포함되어 있는지 검사. 결과가 0이 아니면 장애물. |
    | **Null 체크 위치?** | State에서 직접 `Visual` 호출 시 `?.` 사용. 또는 Controller 래퍼 메서드(`StopMoving()`)로 위임. |

*   **기술적 포인트 (Senior's Review)**
    *   **Generic Reusability**: 한 번 정의한 `StateMachine<TState>`를 Player/Boss가 공유함으로써 DRY 원칙 준수.
    *   **IState Interface**: `Enter()`/`Exit()` 계약을 분리하여 StateMachine이 구체 타입에 의존하지 않도록 설계.
    *   **Update는 다르게**: Player는 입력이 "필요"하고 Boss는 "필요 없는" 것. 이 의도적 차이를 코드로 명확히 표현하는 것이 과도한 추상화보다 우선.
    *   **Visual 통합**: Animation Event 수신과 비주얼 효과를 한 클래스에서 관리하여 응집도(Cohesion) 향상.
    *   **Defensive Programming**: 외부 의존성(Visual)이 없어도 핵심 로직은 동작해야 함.
    *   **Graceful Degradation**: 비주얼 컴포넌트 없이도 게임 로직은 정상 실행.
    *   **LayerMask 이해**: Unity 물리 시스템의 핵심. 비트 연산으로 효율적인 레이어 필터링.




---

### **2026-02-10 (화): Combat System 구현 & 프로젝트 구조화**


*   **작업 내용**

    **플레이어 피격 및 사망 로직 구현**
    *   `HitState` 구현: 중력 적용(공중 피격 시 부유 방지), 무적 시간, `CrossFade` 애니메이션.
    *   `HandleDeath`에 `StopAllCoroutines()` 추가하여 사망 후 무적 코루틴 잔존 방지.
    *   점프 전환 비활성화 (애니메이션 부재, `MoveState` 주석 처리).
    *   `BaseVisual`: `TriggerHit`/`TriggerDie` CrossFade 방식으로 변경.

    **보스 공격 시스템 및 전략 패턴(Strategy Pattern) 적용**
    *   `BossAttackState` 구현: `IBossAttackPattern`에 위임하는 범용 실행기로 설계 (Strategy Pattern).
    *   `BasicAttackPattern`: 기존 근접 공격 로직 이관 (애니메이션 + DamageCaster + 타이머).
    *   `BossCombatState`: attackRange 내 + 쿨다운 완료 시 공격 전환.

    **프로젝트 구조화**
    *   폴더 구조를 `Common`, `Player`, `Boss`로 분리. `BossRaid` → `Core` 네임스페이스 리팩토링.
    *   `PlayerController.cs` 이동 및 중복 폴더(`Patterns`, `Interfaces`, `Combat`) 정리.

    **기술 문서 및 아키텍처 최신화**
    *   `System_Blueprint.md`: Boss AI 클래스 다이어그램에 `BossAttackState`, `IBossAttackPattern`, `DamageCaster` 추가.
    *   `Technical_Glossary.md`: Strategy Pattern, Invincibility Frame 등 용어 추가 및 섹션 재배치.

*   **기술적 포인트 (Senior's Review)**
    *   **Strategy Pattern (OCP)**: 공격 패턴을 인터페이스로 추상화하여 `BossAttackState`가 패턴 내용을 몰라도 실행 가능. 새 패턴 추가 시 기존 코드 수정 불필요.
    *   **Defensive Exit**: `BossAttackState.Exit()`에서 `DisableHitbox()` 호출로 사망 등 강제 전환 시 유령 데미지 방지.
    *   **Coroutine Cleanup**: 사망 시 `StopAllCoroutines()`로 잔존 코루틴이 죽은 객체에 부작용을 일으키는 것을 방지.
    *   **Namespace Strategy**: `Core` 네임스페이스로 물리적 위치(폴더)와 논리적 위치(네임스페이스)를 일치시켜 유지보수성 향상.

---

### **2026-02-11 (수): Dragon Asset Migration & Strategy Pattern 확장**

*   **작업 내용**

    **Asset Migration (Cube → Dragon)**
    *   기존 Cube 보스를 드래곤 모델로 전면 교체.
    *   Unity Animator에 Blend Tree 구축: Idle/Walk 모션 혼합, Threshold 수동 설정 `0`(Idle), `3.5`(Walk).
    *   `CharacterController` Radius/Height(1/1) 조정으로 공중 부양(Floating) 이슈 해결.
    *   플레이어와 동일한 중력(`Physics.gravity`) 로직 적용.

    **Strategy Pattern 확장 (ClawAttackPattern)**
    *   `IBossAttackPattern`을 구현한 `ClawAttackPattern` 신규 추가.
    *   로직: 타겟 회전 → Claw Attack 애니메이션 → Hitbox 활성화(데미지 1.5배) → 돌진(Rush) → 정지.
    *   **OCP 입증**: `BasicAttackPattern`에 이어 2번째 패턴을 추가했지만, `BossAttackState.cs`는 **단 한 줄도 수정하지 않음**.
    *   블로그: [🧠 OCP를 지키는 보스 패턴 설계](file:///d:/Unity-projects/BossRaidPortfolio/docs/blog/0211_Boss_Pattern_Design.md)

    **Compound Collider & 피격 구조**
    *   이동용 `CharacterController`와 피격용 `CapsuleCollider`(Head, Body, Tail — Bone 부착) 역할 분리.
    *   `BossHitBox` → `Health` 중계 구조: 드래곤의 각 부위 콜라이더가 본체 HP로 데미지 위임.
    *   `DamageCaster` 중복 피격 방지: Owner `InstanceID` 추적으로 단일 프레임 다중 히트 무시.
    *   블로그: [⚡ 화려한 드래곤 뒤에 숨겨진 최적화 기술](file:///d:/Unity-projects/BossRaidPortfolio/docs/blog/0211_Physics_Optimization.md)

    **Feature Toggles & 디버그 도구**
    *   `enableChase`, `enableRotation`, `enableBasicAttack`, `enableClawAttack` 등 기능별 On/Off 인스펙터 토글 추가.
    *   Raycast 시각화(Gizmo) 색상 변경으로 디버깅 효율 향상.

*   **기술적 포인트 (Senior's Review)**
    *   **Asset Migration Strategy**: 단순 모델 교체가 아닌, 물리(Controller)와 피격(Collider)을 분리하여 유지보수성 확보. 드래곤을 다른 모델로 바꿔도 콜라이더 구조만 재배치하면 됨.
    *   **Compound Collider vs Mesh Collider**: Mesh Collider의 오버헤드를 피하고, Bone을 따라가는 Primitive Collider 조합으로 성능 최적화.
    *   **Strategy Pattern → OCP**: 새 공격 패턴 100개를 추가해도 `BossAttackState` 코드는 수정 불필요. 기획자의 요청이 들어올 때마다 패턴 클래스만 추가하면 되는 구조.
    *   **NonAlloc API**: `OverlapSphereNonAlloc` + 사전 할당 배열로 GC Spike 원천 차단. 네트워크 동기화 시에도 프레임당 메모리 할당 0 유지.

---

### **2026-02-12 (목): Documentation Sync & Boss Pattern Polishing**

*   **작업 내용**
    *   **System_Blueprint 정합성 검증**: 3개 다이어그램(Player, Boss AI, Attack Strategy)을 실제 코드와 대조하여 **6건의 불일치** 발견 및 수정 완료.
        *   `BossVisual` 메서드 교정, `BossHitBox` 클래스 추가, `Health` 이벤트 반영, `DamageCaster.DisableHitbox()` 추가 등.
    *   **문서화 부채 전수 점검**: 6개 기술 문서를 2/11 작업 결과와 대조. Medium 4건·Low 2건 부채 발견 후 5건 수정.
        *   `Boss_Algorithm_Design.md`: 제목 변경, §8 Compound Collider 피격 구조, §9 Feature Toggle 추가.
        *   `Animator_Setup_Guide.md`: §5 Boss Dragon Animator 설정 추가.
        *   `Animation_Implementation_Log.md`: §6 Boss 애니메이션 구현 기록 추가.
    *   **Hitbox Synchronization (Bone-Synced Hierarchy)**:
        *   **Basic Attack**: Head Bone 자식에 `DamageCasterPlace` 오브젝트를 생성하여 `DamageCaster`와 연결, 애니메이션에 따라 판정 위치 자동 동기화.
        *   **Claw Attack**: 보스의 `Head`, `Body`, `Tail` 피격 콜라이더를 모델의 본(Bone) 하위 계층으로 이동하여, 도약 공격 시 몸체와 판정 범위가 정확히 일치하도록 개선.
        *   **Refactoring**: `DamageCaster`를 배열 대신 명시적 필드(`HeadDamageCaster`, `ClawDamageCaster`)로 분리하여 코드 가독성 향상.
    *   **Claw Attack Polishing**:
        *   **Animation State Fix**: Animator State 이름 불일치로 인한 전환 실패 문제 해결 및 `CrossFade` 안정성 확보.
        *   **Partial Animation Logic**: `exitPhaseRatio`(0.5)를 도입하여 공격 애니메이션의 도약(Rush) 부분만 재생 후 즉시 복귀, 복귀 모션을 생략하여 타격감 개선.
        *   **MoveRaw() 메서드**: 공격 중 이동 시 `PlayMove`(Walk 애니메이션)가 호출되어 공격 모션을 덮어씌우는 문제를 해결하기 위해, 애니메이션 간섭 없는 순수 물리 이동 메서드 구현.

*   **기술적 포인트 (Senior's Review)**
    *   **Bone-Synced Hitbox**: `DamageCaster._castCenter`를 애니메이션이 움직이는 Bone 자식 Transform으로 설정하면, `OverlapSphereNonAlloc`의 원점이 매 `FixedUpdate`마다 Bone 위치를 자동 추적. 코드 수정 없이 물리 판정과 애니메이션 동기화 달성.
    *   **Animation-Driven Logic Control**: 애니메이션의 특정 시점(Normalized Time)을 로직의 트리거로 사용하여, 비주얼과 로직의 완벽한 동기화 구현. (0.3까지 돌진, 0.5에서 조기 종료)
    *   **Separation of Move & Animate**: `MoveTo`(이동+애니)와 `MoveRaw`(이동만)를 분리하여, 상황에 맞는 이동 방식 선택 가능. FSM의 유연성 확보.
    *   **SoC(관심사 분리) 준수**: `BossHitBox`(피격 수신)에 공격 로직을 합치려는 시도를 배제하고, `DamageCaster`(공격 발신) 컴포넌트를 별도 유지하여 Hurtbox/Hitbox 책임 분리 원칙 준수.

*   **기술적 고민**
    *   **Animation Event vs Code Timer**: 애니메이션 이벤트를 심는 방식은 직관적이나 클립 교체 시 재작업 필요. 코드로 `normalizedTime`을 체크하는 방식은 클립이 바뀌어도 비율(%)로 동작하므로 유지보수에 더 유리하다고 판단.

---

### **2026-02-20 (금): AoE 안정화 + Boss 2페이즈 전환 + 버그 수정 통합 로그**

* **오늘 완료한 작업 (통합)**
    * **Pattern 4 (AoE) 안정화**
        * `AoECircleController`의 바닥 밀착 보정(`fallbackYOffset=0`)을 적용해 장판이 지면에 정확히 붙도록 조정.
        * AoE 단계별 임시 디버그 로그를 정리해 콘솔 노이즈를 감소.
    * **Pattern 4 (AoE) 진행 방향 예측 고도화**
        * `ResolveImpactPoint()`를 타겟 진행 방향 기반 예측 확산 방식으로 확장.
        * 파라미터(`headingLeadTime`, `maxHeadingLeadDistance`, `forwardSpreadRadius`, `sideSpreadRadius`, `headingBias`, `headingMinSpeed`)를 도입해 인스펙터 튜닝 가능하도록 구성.
    * **Boss 2페이즈 전환 구현**
        * `HealthRatio <= 0.5`에서 `Phase1 -> Phase2` 단방향 전환.
        * 각 페이즈 시작 시 `Scream` 인트로를 1회 재생하고, 인트로 종료 후 공격 허용.
    * **페이즈별 패턴 선택 안정화**
        * Phase 1: `Basic(Head)` + `Lunge`
        * Phase 2: `Projectile` + `AoE`
        * 원거리 강제 AoE 분기를 제거하고 공통 선택기로 통합.
        * 두 패턴이 모두 활성일 때 즉시 동일 패턴 연속 선택을 방지(no-immediate-repeat).
    * **디버그 가시성 강화**
        * `showPhaseDebugLabel` 토글 추가.
        * 표시 항목: 현재 페이즈, HP%, Intro 재생 여부, Phase2 트리거 여부.
    * **문서/인코딩 정리**
        * `BossController.cs`의 한글 주석/툴팁 인코딩 깨짐 복구(UTF-8).
    * **버그 수정 (누락 보강)**
        * AoE 장판이 지면에서 떠 보이거나 파묻히던 위치 오차를 `AoECircleController`의 `fallbackYOffset=0` 보정으로 해결.
        * 페이즈 인트로(`Scream`) 재생 중 공격 패턴이 조기 선택되던 타이밍 경쟁 이슈를 전환 구간 잠금으로 수정.
        * 패턴 2종 활성 시 동일 패턴이 즉시 반복 선택되던 편중 문제를 no-immediate-repeat 규칙으로 수정.
        * 사거리 기반 강제 AoE 분기로 Phase 패턴 의도가 흐려지던 선택 오류를 공통 선택기 통합으로 정리.

* **기술적 고려 / 회고**
    * **전환 구간 잠금의 필요성**: 페이즈 인트로(`Scream`) 중 공격 선택을 잠가 연출과 FSM 전환 타이밍을 일치시킴.
    * **선택기 단일화**: 사거리 조건별 별도 선택 경로는 패턴 편중을 유발하므로, 공통 선택기로 통합해 예측 가능한 순환을 확보.
    * **튜닝 비용 절감**: AoE 예측 파라미터를 인스펙터화해 코드 수정 없이 난이도와 적중률 조절 가능.

* **검증**
    * `dotnet build BossRaidPortfolio.sln` 빌드 성공(에러 0).


## 📈 2월 마일스톤: 싱글플레이 로직 완성 (Capsule vs Cube)

> **목표**: 클라이언트 구축

### 1주차: 플레이어 컨트롤러 & 전투 시스템 (The Capsule & Sword) ✅
- [x] **Input System**: 키보드/마우스 및 패드 입력을 인터페이스화해서 분리.
- [x] **FSM (State Machine)**: `PlayerState` 클래스 기반의 상태 머신 (Idle, Move, Attack, Dash).
- [x] **이동 로직**: `CharacterController`를 이용한 물리 기반 이동.
- [x] **핵심**: 입력과 로직 분리 완료. (네트워크 입력 교체 대비)
- [x] **에셋 교체**: `CombatGirls_KatanaCharacterPack` 연동 및 `Visual` 계층 분리 (`Animator_Setup_Guide.md` 준수).
- [x] **Animator 설정**: `Locomotion`, `Attack1~3`, `Jump`, `Quickshift_F` 상태 연결.
- [x] **Hitbox 시스템**: 칼이 휘둘러질 때 특정 프레임에서만 판정이 생기도록 설계.
- [x] **피격 시스템**: 플레이어/보스가 데미지를 받았을 때의 반응 (HP 감소, 피격 애니메이션, 무적 시간).
- [x] **Damage 클래스**: `IDamageable` 인터페이스 (보스/잡몹 공통 상속).
- [x] **판정 최적화**: `Physics.OverlapSphereNonAlloc` 사용.
- [x] **핵심**: 코드가 판정 시점을 정확히 계산하는 것을 보여줘야 함.


---

### 2주차: 보스 AI 패턴 (The Cube)

#### 보스 FSM 아키텍처 ✅
- [x] **StateMachine 제네릭화**: `StateMachine<TState>`로 Player/Boss 공용 상태 머신 통합.
- [x] **BossBaseState**: `BaseState<BossController>` 상속, `Update()` 입력 없이 내부 로직만 처리.
- [x] **IState 인터페이스**: `Enter()`/`Exit()` 공통 계약 정의로 StateMachine의 구체 타입 의존 제거.
- [x] **BossVisual 분리**: 애니메이션/UI 제어를 `BossVisual` 클래스로 이관하여 SRP 준수.

#### 피격 로직 (Player & Boss 공통) ✅
- [x] **IDamageable 인터페이스**: 공격자가 타겟 타입을 몰라도 데미지 전달 가능.
- [x] **Health 컴포넌트**: HP 관리, `OnDamage`/`OnDie` 이벤트 발행.
- [x] **DamageCaster**: `OverlapSphereNonAlloc`으로 GC-Free 타격 판정.
- [x] **Event-Driven Death**: `OnDie` 이벤트 구독으로 `DeadState` 전환.

#### 보스 행동 패턴 🔄
- [x] **패턴 1 (추적)**: 플레이어와의 거리 계산, 적정 거리 유지 및 공격 유도.
- [x] **근접 공격 (BasicAttackPattern)**: `IBossAttackPattern` Strategy Pattern 적용. 쿨다운 시스템.
- [x] **패턴 2 (돌격)**: 예고 표시 후 돌진.
- [ ] **패턴 3 (투사체)**: 큐브에서 작은 큐브(미사일) 발사.

---

### 3주차: 보스 행동 패턴 & 시스템 최적화 & 게임 루프 (The Logic)

#### 보스 행동 패턴 🔄
- [x] **패턴 3 (투사체)**: 큐브에서 작은 큐브(미사일) 발사 및 애셋 입히기.
- [x] **패턴 4 (장판)**: 하늘에서 땅을 향해 불 발사. 

#### 시스템 최적화 & 게임 루프 (The Logic)
- [x] **오브젝트 풀링**: 미사일/이펙트를 Zero-Allocation으로 관리.

---

### 4주차: 보스 행동 패턴 & 시스템 최적화 & 게임 루프 (The Logic)
#### 시스템 최적화 & 게임 루프 (The Logic)
- [ ] **UI 시스템**: HP 바, 보스 페이즈 알림 등 코드 제어.
- [ ] **게임 매니저**: 시작 → 전투 → 페이즈 전환 → 승리/패배 흐름 제어.
---

#### 🚧 폴리싱 
- [ ] **(플레이어)대쉬 방향 수정**: 공격 방향이 아닌 키보드 입력 방향으로 대쉬.
- [ ] **피격 플래시 이펙트**: `BaseVisual.FlashRoutine`을 Emission 기반으로 변경 (`material.SetColor("_EmissionColor")`) 또는 머티리얼 교체 방식 적용.
- [ ] **(보스)Run/Walk 애니매이션 추가**: Run 애니메이션 추가 및 그에 맞는 보스 움직임 속도 부드럽게 증가시키기
- [ ] **(플레이어)넉백 추가하기**: 플레이어가 보스에게 피격당했을 때 넉백 추가하기
- [ ] **다중 레이캐스트 탐지**: 눈 위치(몸통 1/2~머리 중간)에서 여러 방향 감지.
- [ ] **추적 알고리즘 검토**: NavMesh / A* 경로탐색 적용 여부 결정.
- [ ] **(보스)피격 중 공격 시전 유지**: 피격 애니메이션에 의해 공격 시전이 끊기지 않도록 하고, 데미지는 정상 반영.
- [ ] **(보스)패턴 3 투사체 조준 보정**: 플레이어가 드래곤 뒤쪽에 있어도 플레이어 방향으로 투사체 발사.
- [ ] **(보스)패턴별 동적 공격 사거리 적용**: 패턴 1은 근거리 사거리, 그 외 패턴은 더 긴 사거리 사용.


---

### 📝 공통 문서화 작업
- [ ] **책임(Responsibility) 문서화**: 로직별 책임 소재를 코드와 글로 명확히 설명 (면접 대비).
---

### **2026-02-20 (금: Unity 버전 롤백 후 패키지 복구)**

* **오늘 완료한 작업**
* Packages/manifest.json을 Unity 2022.3 기준으로 정리 (URP/VFX 14.0.12, UGUI 1.0.0, TMP 3.0.6 추가).
* Unity 6 전용/미지원 의존성(com.unity.multiplayer.center, com.unity.modules.accessibility, com.unity.modules.adaptiveperformance, com.unity.modules.vectorgraphics) 제거.
* 오염된 Packages/packages-lock.json을 삭제해 Unity 2022에서 잠금 파일을 재생성하도록 복구 경로 정리.

* **기술적 고려**
* ProjectVersion만 되돌리면 패키지 그래프 불일치가 남을 수 있어, manifest와 lock을 함께 복구해야 컴파일/에셋 임포트 오류 연쇄를 차단할 수 있다.

### **2026-02-20 (금: AoE Rigidbody API 호환 수정)**

* **오늘 완료한 작업**
* AoEAttackPattern.ResolveTargetHeading()에서 Rigidbody.linearVelocity 사용 코드를 Unity 2022 호환 Rigidbody.velocity로 교체.
* Unity 6 API 잔재로 인해 발생하던 CS1061 컴파일 오류를 제거.

* **기술적 고려**
* 컴파일 실패 상태에서는 어셈블리 생성(Assembly-CSharp-Editor)이 깨져 다수 스크립트가 Missing처럼 보일 수 있으므로, 우선 1차 컴파일 에러를 제거해 어셈블리 복구를 우선한다.

### **2026-02-20 (금: Editor 어셈블리 앵커 추가)**

* **오늘 완료한 작업**
* `Assets/Editor/EditorAssemblyAnchor.cs`를 추가해 Assembly-CSharp-Editor 어셈블리 생성 경로를 고정.
* Burst 엔트리포인트 스캔 단계에서 발생하던 Assembly-CSharp-Editor 해석 실패 리스크를 완화.

