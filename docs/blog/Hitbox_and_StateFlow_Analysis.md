# 🗡️ Technical Deep Dive: Garbage-Free Hitbox & State Machine Execution Flow

## 📌 Overview
본 포스트에서는 **Hitbox & Damage System**의 구현 과정과 FSM(Finite State Machine)의 상태 전환 시 발생하는 **Frame-by-Frame Execution Flow**를 심층 분석한다. 단순한 기능 구현을 넘어 **메모리 최적화(Zero-GC)**와 **실행 흐름의 정밀성**을 확보하기 위한 설계 의도를 기술한다.

---

## 🏗️ 1. Hitbox System: Garbage-Free Design

전투 액션 게임에서 매 프레임 발생하는 공격 판정은 성능 병목의 주요 원인이 된다. Unity의 물리 엔진을 활용하되, 가비지 컬렉터(GC) 부하를 원천 차단하는 설계를 적용하였다.

### 1-1. 문제점: `Physics.OverlapSphere` (Alloc)
기본적인 물리 감지 API인 `Physics.OverlapSphere`는 호출 시마다 새로운 충돌체 배열(`Collider[]`)을 힙(Heap) 메모리에 할당하여 반환한다.
```csharp
// ❌ 비효율: 매 프레임 가비지(Garbage) 생성
Collider[] hitColliders = Physics.OverlapSphere(center, radius, layerMask);
```
공격 모션 중 이 함수가 반복 호출될 경우 다량의 가비지가 생성되어 GC 스파이크(프레임 드랍)를 유발한다.

### 1-2. 해결책: `Physics.OverlapSphereNonAlloc`
**Non-Alloc API**를 사용하여 메모리 할당 문제를 해결하였다.
```csharp
// ✅ 최적화: 미리 할당된 배열을 재사용
int hitCount = Physics.OverlapSphereNonAlloc(_center, _radius, _hitResults, _layerMask);
```
*   **Pre-allocation**: `_hitResults` 배열을 클래스 초기화 시점에 한 번만 할당하고 런타임에 재사용한다.
*   **Result**: 공격 판정 시 **런타임 메모리 할당량 0Byte(Zero-GC)**를 달성하였다.

---

## 🔄 2. State Machine Deep Dive: Execution Flow Analysis

FSM에서 상태 전환(`ChangeState`) 시 코드의 실행 흐름을 명확히 이해하는 것은 정교한 로직 제어를 위해 필수적이다. 특히 '상태가 바뀌었음에도 현재 함수 내의 남은 코드가 실행되는 현상(Synchronous Execution)'에 주목해야 한다.

### 2-1. 분석 시나리오
*   **상황**: 플레이어가 공격 중(`AttackState`) 후딜레이 캔슬 지점에서 대시(`Dash`)를 입력함.
*   **코드 구조**:
    ```csharp
    if (input.HasFlag(InputFlag.Dash)) {
        StateMachine.ChangeState(DashState); // (A) 상태 변경
        Debug.Log("Canceled!");              // (B) 로그 출력
        return true;                         // (C) 제어권 반환
    }
    ```

### 2-2. Frame-by-Frame Trace

#### 🎞️ [Frame 100] (상태 교체 시점)
`ChangeState`가 호출되는 프레임의 실행 로직은 다음과 같다.

1.  **ChangeState 내부 실행**: `AttackState.Exit()` 호출 후 `_currentState` 변수가 `DashState`로 교체되며, 곧바로 `DashState.Enter()`가 실행된다.
2.  **호출 지점으로의 복귀**: `ChangeState` 함수가 종료되면 실행 흐름은 **호출 지점(A)의 다음 라인(B)으로 복귀한다.** 상태 변수가 바뀌었다고 해서 현재 실행 중인 함수의 스택 정보가 사라지지 않기 때문이다.
3.  **잔여 로직 완결**: `Debug.Log`가 출력되고 `return true`를 실행하며 `AttackState.Update`가 종료된다.

#### 🎞️ [Frame 101] (새로운 상태의 Update 시작)
실제적인 대시 로직이 동작하는 다음 프레임의 흐름이다.

1.  **Update Loop**: `PlayerController.Update`가 호출된다.
2.  **State Delegation**: `_stateMachine.Update()`가 호출된다.
3.  **New State execution**: `_currentState`가 이미 **`DashState`**를 가리키고 있으므로, 이제 `DashState.Update()`가 실행되어 이동 및 타이머 로직이 구동된다.

### 💡 기술적 인사이트
> "ChangeState는 **선수 교체 수속**이지, 경기 그 자체가 아니다."
> 상태 전환 즉시 모든 로직이 바뀌는 것이 아니라, 교체된 상태의 로직은 **다음 프레임(Next Frame)**부터 본 궤도에 오른다는 점을 정확히 인지해야 한다.

---

## 📝 결론

*   **최적화**: `NonAlloc` API를 활용하여 모바일 등 제한된 환경에서도 안정적인 프레임을 유지할 수 있도록 설계하였다.
*   **실행 흐름**: 상태 머신의 전환이 **동기적(Synchronous)**으로 발생함을 이해하고, 프레임 단위의 로직 공백이 발생하지 않도록 구현하였다.
*   **확장성**: `IDamageable` 인터페이스를 통해 시스템 간 결합도를 낮추고 전투 시스템의 유지보수성을 극대화하였다.
