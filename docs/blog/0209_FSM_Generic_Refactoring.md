# 🎮 FSM 제네릭 리팩토링: Clean Code로 Player와 Boss 통합하기

Unity 게임 개발에서 FSM(Finite State Machine)은 캐릭터 행동 관리의 핵심이다. 이번 글에서는 Player와 Boss가 각각 별도의 StateMachine을 사용하던 구조를 **제네릭으로 통합**하면서 얻은 인사이트를 공유한다.

---

## 1. 문제: 중복된 StateMachine

처음에는 Player 전용 `StateMachine`과 Boss 전용 `BossStateMachine`이 따로 존재했다.

```csharp
// Player 전용 (기존)
public class StateMachine
{
    private PlayerBaseState _currentState;
    
    public void ChangeState(PlayerBaseState newState) { ... }
    public void Update(PlayerInputPacket input) { ... }
}

// Boss 전용 (기존) - 사실상 동일한 로직
public class BossStateMachine
{
    private BossBaseState _currentState;
    
    public void ChangeState(BossBaseState newState) { ... }
    public void Update() { ... }
}
```

**문제점**: 로직은 동일한데 타입만 다름 → **DRY 원칙 위반**

---

## 2. 해결: 제네릭 StateMachine

`StateMachine<TState>`로 통합하여 Player/Boss 모두 사용 가능하게 변경했다.

```csharp
namespace BossRaid.Patterns
{
    public class StateMachine<TState> where TState : class
    {
        public TState CurrentState { get; private set; }

        public void ChangeState(TState newState)
        {
            // 현재 상태 Exit
            if (CurrentState is IState exitState)
                exitState.Exit();

            CurrentState = newState;

            // 새 상태 Enter
            if (CurrentState is IState enterState)
                enterState.Enter();
        }
    }

    public interface IState
    {
        void Enter();
        void Exit();
    }
}
```

**핵심 변경점**:
- `TState`는 타입 파라미터 → 어떤 State 타입이든 수용
- `IState` 인터페이스로 `Enter()`/`Exit()` 계약 정의
- `Update()` 호출은 각 Controller가 직접 처리

**사용 예시**:
```csharp
// PlayerController
private StateMachine<PlayerBaseState> _stateMachine;
_stateMachine.CurrentState?.Update(input);  // 입력 전달

// BossController
private StateMachine<BossBaseState> _stateMachine;
_stateMachine.CurrentState?.Update();  // 입력 없음
```

---

## 3. Q&A: 왜 BaseState도 제네릭인가?

### 질문: 비제네릭 BaseState와 제네릭 BaseState<TController>의 차이는?

**비제네릭 (기존)**:
```csharp
public abstract class BaseState
{
    protected PlayerController Controller;  // 👈 하드코딩됨! Boss 사용 불가

    public BaseState(PlayerController controller)
    {
        Controller = controller;
    }
}
```

**제네릭 (현재)**:
```csharp
public abstract class BaseState<TController> : IState
{
    protected TController Controller;  // 👈 타입 파라미터로 유동적

    public BaseState(TController controller)
    {
        Controller = controller;
    }

    public abstract void Enter();
    public abstract void Exit();
}
```

| 비교 | 비제네릭 | 제네릭 |
|------|----------|--------|
| Controller 타입 | `PlayerController` 고정 | `TController`로 유동적 |
| Boss 사용 | ❌ 불가능 | ✅ 가능 |
| 재사용성 | 낮음 | 높음 |

---

## 4. Q&A: PlayerBaseState와 BossBaseState를 통합할 수 없나?

### 질문: Update 메서드도 제네릭으로 통합하면 더 깔끔하지 않나?

**현재 구조**:
```csharp
// Player: 입력이 필요
public abstract class PlayerBaseState : BaseState<PlayerController>
{
    public abstract void Update(PlayerInputPacket input);
}

// Boss: 입력 불필요
public abstract class BossBaseState : BaseState<BossController>
{
    public abstract void Update();  // 파라미터 없음
}
```

**통합 시도 (안티패턴)**:
```csharp
public struct NoInput { }  // 의미 없는 빈 구조체

public abstract class BaseState<TController, TInput>
{
    public abstract void Update(TInput input);
}

// Boss: Update(NoInput _) ← 왜 파라미터가 있는데 안 쓰지?
```

### 결론: **분리 유지가 Clean Code**

**이유**:

1. **KISS 위반**: `NoInput`은 타입 시스템을 속이기 위한 우회책
2. **의도 모호화**: 
   - Player는 입력이 **"필요"**
   - Boss는 입력이 **"필요 없음"**
   - 이 차이를 코드로 명확히 표현해야 함
3. **과도한 추상화 지양**: 실제로 다른 것은 다르게 표현하라

> **Clean Code 원칙**: DRY보다 **의도의 명확성**이 우선이다.

---

## 5. Q&A: TController에서 T는 무엇인가?

**T = Type (타입)**

C# 제네릭 관례에서 타입 파라미터 이름은 `T`로 시작한다:

| 이름 | 의미 |
|------|------|
| `T` | 일반 타입 |
| `TController` | Controller 타입 |
| `TState` | State 타입 |
| `TKey`, `TValue` | Dictionary 등에서 사용 |

```csharp
// "아무 Controller 타입이나 넣어라"
public abstract class BaseState<TController>
//                              ↑ "Type of Controller"
```

---

## 6. 최종 아키텍처

```
StateMachine<TState>  (공용)
├─ StateMachine<PlayerBaseState>  ← PlayerController
└─ StateMachine<BossBaseState>    ← BossController

BaseState<TController> : IState  (공용)
├─ PlayerBaseState : BaseState<PlayerController>
│   └─ Update(PlayerInputPacket input)
└─ BossBaseState : BaseState<BossController>
    └─ Update()
```

---

## 7. 핵심 교훈

| 원칙 | 적용 |
|------|------|
| **DRY** | `StateMachine<TState>`로 중복 제거 |
| **SRP** | StateMachine은 상태 전환만 담당, Update는 Controller가 호출 |
| **KISS** | 시그니처가 다르면 억지로 통합하지 않음 |
| **명확성** | Player는 입력 필요, Boss는 불필요 → 코드로 명시 |

제네릭은 강력하지만, **모든 것을 통합하려는 유혹**에 빠지지 않는 것이 Clean Code다.
