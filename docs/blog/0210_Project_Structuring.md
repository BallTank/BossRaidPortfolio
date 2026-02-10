# 🏗️ 모듈성 강화를 위한 프로젝트 구조화와 네임스페이스 전략 (Project Structuring)

> **"코드가 늘어날수록 길을 잃기 쉽다면, 지도가 잘못된 것이다."**

프로젝트 초기에는 파일 몇 개로 시작하지만, 기능이 추가될수록 `Assets/Scripts` 폴더는 금세 혼잡해진다. 금일 `Boss Raid Portfolio` 프로젝트의 스크립트 구조를 대대적으로 리팩토링했다. 왜 이 작업이 필요했으며, 어떤 기준으로 구조를 잡았는지 기술한다.

## 1. Before: 혼재된 책임과 모호한 경계

초기 구조는 기능 구현에 급급하여 물리적 위치와 논리적 소속이 일치하지 않았다.

*   `Assets/Scripts/` 루트에 `PlayerController.cs`, `GameManager.cs` 등이 섞여 있었다.
*   `Patterns`, `Interfaces`, `Combat` 같은 폴더들이 최상위에 존재하여, 해당 기능이 누구를 위한 인터페이스인지 불분명했다.
*   네임스페이스 `BossRaid`를 전역적으로 사용하여, `BossRaid.PlayerController`와 `BossRaid.Utilities`가 같은 레벨에서 혼재되었다.

**문제점**: 특정 스크립트가 플레이어 전용인지, 보스도 공유하는 공통 로직인지 파일명이나 위치만으로 파악하기 어려웠다. 이는 협업 시 코드의 오용(Misuse)을 유발할 수 있는 요인이 된다.

## 2. Refactoring: 물리적/논리적 구조의 일치

**"물리적 위치(폴더)가 곧 논리적 소속(네임스페이스)이어야 한다"**는 대원칙 하에 구조를 재편했다.

### 2.1. 폴더 구조 (Physical Structure)

스크립트를 크게 3가지 대분류로 나누어 관리한다.

```text
Assets/Scripts/
├── Core/
│   ├── Common/    (모든 캐릭터가 공유하는 로직: Health, DamageCaster, FSM Base)
│   ├── Player/    (플레이어 전용: Controller, Input, States, Visual)
│   └── Boss/      (보스 전용: Controller, AI FSM, Attacks, Visual)
```

이로써 `Assets/Scripts/Player/` 내부에 위치한 파일은 플레이어 전용 로직임을 명시적으로 나타낸다.

### 2.2. 네임스페이스 전략 (Logical Structure)

물리적 구조에 맞춰 네임스페이스를 `Core` 최상위 계층으로 구조화했다.

*   `BossRaid` (기존) → **`Core`**
*   `Core.Common`: `StateMachine<T>`, `IDamageable`, `Health` 등 재사용 가능한 기반 클래스군.
*   `Core.Player`: `PlayerController`, `MoveState` 등 플레이어별 구체 구현체.
*   `Core.Boss`: `BossController`, `BossAttackState` 등 보스별 구체 구현체.

```csharp
// Example: BossController.cs
namespace Core.Boss
{
    using Core.Common; // 공통 모듈 참조
    using Core.Player; // 타겟(플레이어) 참조

    public class BossController : MonoBehaviour { ... }
}
```

## 3. Result: 명확해진 소속감

구조화의 효과는 다음과 같이 요약할 수 있다.

1.  **가독성 향상**: IDE에서 클래스 이름만으로 (`Core.Boss.BossController`) 해당 코드의 소속과 역할을 즉시 파악할 수 있다.
2.  **의존성 제어**: `Core.Common`이 `Core.Player`를 참조해서는 안 된다는 규칙을 네임스페이스 레벨에서 직관적으로 관리할 수 있다. 이는 순환 참조 방지에 기여한다.
3.  **유지보수성**: 신규 기능을 추가할 때 파일 생성 위치에 대한 고민이 사라진다. 가령 보스 공격 패턴은 예외 없이 `Core/Boss/Attacks/`에 위치하게 된다.

작은 프로젝트일지라도 초기에 견고한 폴더 및 네임스페이스 규칙을 수립하는 것은, 향후 발생할 수 있는 스파게티 코드를 예방하고 시스템의 확장성을 담보하는 가장 중요한 투자임을 확인했다.
