# 🚀 Progress Log: Boss Raid Portfolio

## 📅 [2월 1주차] 목표: 플레이어 컨트롤러 및 상태 머신 (The Capsule)

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

## 📈 2월 마일스톤: 싱글플레이 로직 완성 (Capsule vs Cube)

> **목표**: 클라이언트 구축

### 1주차: 플레이어 컨트롤러 & 상태 머신 (The Capsule) ✅
- [x] **Input System**: 키보드/마우스 및 패드 입력을 인터페이스화해서 분리.
- [x] **FSM (State Machine)**: `PlayerState` 클래스 기반의 상태 머신 (Idle, Move, Attack, Dash).
- [x] **이동 로직**: `CharacterController`를 이용한 물리 기반 이동.
- [x] **핵심**: 입력과 로직 분리 완료. (네트워크 입력 교체 대비)

### 2주차: 전투 시스템 & 판정 (The Capsule Sword) 🔄
- [/] **에셋 교체**: `CombatGirls_KatanaCharacterPack` 연동 및 `Visual` 계층 분리 (`Animator_Setup_Guide.md` 준수).
- [/] **Animator 설정**: `Locomotion`, `Attack1~3`, `Jump`, `Quickshift_F` 상태 연결.
- [ ] **Hitbox 시스템**: 칼이 휘둘러질 때 특정 프레임에서만 판정이 생기도록 설계.
- [ ] **피격 시스템**: 플레이어/보스가 데미지를 받았을 때의 반응 (HP 감소, 피격 애니메이션, 무적 
- [ ] **Damage 클래스**: `IDamageable` 인터페이스 (보스/잡몹 공통 상속).
시간).
- [ ] **판정 최적화**: `Physics.OverlapSphereNonAlloc` 사용.
- [ ] **핵심**: 코드가 판정 시점을 정확히 계산하는 것을 보여줘야 함.

### 3주차: 보스 AI 패턴 (The Cube)
- [ ] **패턴 1 (추적)**: 플레이어와의 거리 계산, 적정 거리 유지 및 공격 유도.
- [ ] **패턴 2 (돌격)**: 예고 표시 후 큐브 돌진.
- [ ] **패턴 3 (투사체)**: 큐브에서 작은 큐브(미사일) 발사.
- [ ] **핵심**: FSM 또는 Behavior Tree를 코드로 깔끔하게 구현.

### 4주차: 시스템 최적화 & 게임 루프 (The Logic)
- [ ] **오브젝트 풀링**: 미사일/이펙트를 Zero-Allocation으로 관리.
- [ ] **UI 시스템**: HP 바, 보스 페이즈 알림 등 코드 제어.
- [ ] **게임 매니저**: 시작 → 전투 → 페이즈 전환 → 승리/패배 흐름 제어.