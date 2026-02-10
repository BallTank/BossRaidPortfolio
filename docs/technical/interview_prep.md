# 공부한 것
- [x] 공격 및 콤보
- [x] 인풋
- [x] 상태머신
- [] 피격 애니메이션

# 면접 대비 코드 분석 및 예상 질문 가이드

이 문서는 `BossRaidPortfolio` 프로젝트의 아키텍처를 면접관에게 설명하고, 예상되는 기술 질문에 답변하기 위해 작성되었습니다.

## 1. 아키텍처 개요 (Architecture Overview)

이 프로젝트는 **State Pattern (상태 패턴)** 기반으로 한 **Finite State Machine (FSM)** 구조를 채택하고 있습니다. 캐릭터의 동작을 논리적으로 분리하여 관리하며, 확장성과 유지보수성을 높였습니다.

### 핵심 구성 요소
1.  **`PlayerController` (Context)**
    *   Unity의 `MonoBehaviour`를 상속받아 실제 게임 오브젝트에 붙는 컴포넌트입니다.
    *   데이터(`AttackCombos`, `Stats`)와 컴포넌트 참조(`CharacterController`, `Animator`)를 보유합니다.
    *   모든 상태(State)들이 공유하는 **Blackboard** 역할을 수행합니다.
    *   `IDashContext`, `IAttackable` 같은 인터페이스를 구현하여 외부 시스템과의 의존성을 느슨하게 유지합니다.

2.  **`StateMachine` (Manager)**
    *   현재 상태(`_currentState`)를 관리하고 전환(`ChangeState`)을 담당합니다.
    *   `Update` 메서드를 통해 현재 상태의 로직을 매 프레임 실행합니다.

3.  **`BaseState` (Logic Unit)**
    *   모든 상태의 추상 기본 클래스입니다.
    *   `Enter()`, `Update()`, `Exit()`의 생명주기를 정의합니다.
    *   `PlayerController`에 대한 참조를 protected로 가지고 있어, 자식 상태들이 컨트롤러의 데이터에 쉽게 접근할 수 있습니다.

4.  **`PlayerInputPacket` (Data Protocol)**
    *   입력 시스템과 로직의 결합을 끊기 위해 설계된 구조체입니다.
    *   GC(Garbage Collection)를 방지하기 위해 `struct`로 선언되었으며, `InputFlag` 비트 마스킹을 사용하여 버튼 입력을 최적화된 형태로 전달합니다.

---

## 2. 심층 코드 분석 (Deep Dive)

면접관이 "이 부분은 어떻게 동작합니까?"라고 물었을 때 설명할 포인트입니다.

### 2.1. 콤보 공격 시스템 (`AttackState`)
가장 복잡한 로직이므로 질문이 나올 확률이 높습니다.

*   **구조**: 별도의 상태 클래스(`Attack1State`, `Attack2State`)를 만들지 않고, 하나의 `AttackState` 내부에서 `_comboIndex`와 `StartComboStep()` 메서드를 통해 데이터를 교체하는 방식을 사용했습니다.
*   **이유**: 클래스가 너무 많아지는 것을 방지(Class Explosion 방지)하고, 콤보 간의 데이터 공유(예: 이전 공격의 모멘텀 유지 등)를 쉽게 하기 위함입니다.
*   **선입력(Input Buffering)**:
    *   `_reserveNextCombo` 플래그를 사용합니다.
    *   공격 애니메이션 도중(`comboInputWindow` 이내)에 입력이 들어오면 플래그를 켜두고, 애니메이션이 끝나는 시점(`CheckComboTransition`)에 다음 콤보로 넘어갑니다.
    *   이는 액션 게임의 조작감을 향상시키는 핵심 테크닉입니다.

### 2.2. 입력 최적화 (`PlayerInputData`)
*   **비트 마스킹**: `InputFlag` enum에 `System.Flags`를 쓰고 비트 연산(`|`, `&`, `~`)으로 여러 버튼의 상태를 `byte` 하나에 담았습니다.
*   **Struct 사용**: 매 프레임 `Update`에서 입력을 주고받아야 하므로, 힙 할당(Heap Allocation)을 피하기 위해 `class` 대신 `struct`를 사용했습니다. 이는 모바일 환경 등을 고려한 최적화입니다.

---

## 3. 면접 예상 질문 & 답변 (Q&A)

스스로 이해도를 점검해 보세요.

### Q1. (기초) 왜 FSM을 사용했나요? `if-else`로 구현하면 안 되나요?
> **답변 예시**: "물론 `if-else`로도 구현 가능하지만, 상태가 많아질수록 코드가 복잡해지고 관리가 어려워집니다(Spaghetti Code). FSM을 사용하면 각 상태의 로직을 클래스로 분리하여 **단일 책임 원칙(SRP)**을 지킬 수 있고, 특정 상태에서 다른 상태로의 전이 조건을 명확하게 관리할 수 있어서 확장성에 유리합니다."

### Q2. (중급) `AttackState`에서 콤보 공격은 재귀적으로 호출되나요? 스택 오버플로우 위험은 없나요?
> **답변 예시**: "아닙니다. `StartComboStep()` 메서드는 콤보 데이터를 갱신하고 애니메이션을 재생하는 역할을 할 뿐, 자기 자신을 호출하는 재귀 구조가 아닙니다. `Update` 문에서 타이머와 입력을 체크하다가 조건이 만족되면 다음 단계로 넘어가기 위해 `StartComboStep()`을 한 번 호출하고 `return` 합니다. 즉, 매 프레임 로직은 `Update` 루프 내에서 평탄하게(flat) 실행되므로 스택 오버플로우 위험은 없습니다."

### Q3. (중급) `PlayerController`와 State 간의 참조 관계가 강한 것 같은데(Strong Coupling), 이를 개선할 방법은 없나요?
> **답변 예시**: "맞습니다. 현재 `BaseState`가 `PlayerController` 구체 클래스를 알고 있습니다. 더 느슨한 결합(Loose Coupling)을 원한다면, 상태가 필요로 하는 기능만 정의한 인터페이스(예: `IMovementContext`, `IAnimationContext`)를 `PlayerController`가 구현하게 하고, State는 그 인터페이스만 참조하게 하는 방법이 있습니다. 하지만 현재 프로젝트 규모에서는 `PlayerController`가 중추적인 Blackboard 역할을 하므로, 개발 생산성을 위해 직접 참조를 허용했습니다."

### Q4. (고급) `AttackState.Update`에서 `HandleInput`을 `CheckComboTransition`보다 먼저 호출하는 이유는 무엇인가요?
> **답변 예시**: "매우 중요합니다. 만약 순서가 바뀐다면, 콤보가 끝나는 바로 그 프레임(Frame)에 들어온 입력을 놓칠 수 있습니다(Input Eating). 먼저 이번 프레임의 입력을 처리하여 버퍼(`_reserveNextCombo`)에 저장해 둔 뒤, 그 상태를 바탕으로 전이 여부를 판단해야 정확한 입력 판정이 가능합니다. 이는 프레임 단위의 로직 실행 순서(Execution Order)를 고려한 설계입니다."
