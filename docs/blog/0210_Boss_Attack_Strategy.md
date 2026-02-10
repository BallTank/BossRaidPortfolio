# ⚔️ 전략 패턴(Strategy Pattern)을 이용한 확장 가능한 보스 공격 시스템 구현

> **"변화는 피할 수 없다. 그렇다면 변화에 유연한 구조를 설계하라."**

보스 레이드 게임에서 가장 빈번하게 수정되고 추가되는 요소는 단연 '보스의 공격 패턴'이다. 초기 설계 단계에서 이를 신중하게 고려하지 않으면, 새로운 공격이 추가될 때마다 거대한 `switch` 문이나 조건 지옥에 빠지기 쉽다. 금일 `Boss Raid Portfolio` 프로젝트에 적용한 전략 패턴(Strategy Pattern) 기반의 공격 시스템을 기술한다.

## 1. Problem: 하드코딩된 공격 로직의 한계

기존의 `BossAttackState`는 특정 애니메이션 재생, 데미지 판정 시간, 지속 시간 등을 직접 제어하고 있었다.

*   한 종류의 근접 공격만 수행할 때는 문제가 없으나, '돌격', '투사체 발사', '광역기' 등이 추가되면 `BossAttackState` 클래스가 비대해지는 'God Class' 현상이 발생한다.
*   기능 추가 시마다 기존 로우레벨 코드를 수정해야 하므로, 의도치 않은 버그가 발생할 가능성이 높다 (Open-Closed Principle 위반).

## 2. Solution: 행동의 캡슐화 (Strategy Pattern)

공격이라는 '행동'을 인터페이스로 추상화하여, 상태(State) 클래스는 실행 환경만 제공하고 구체적인 로직은 독립된 클래스에 위임하는 구조를 채택했다.

### 2.1. 인터페이스 정의 (IBossAttackPattern)

모든 보스 공격 패턴이 공유해야 할 핵심 생명주기를 인터페이스로 정의한다.

```csharp
public interface IBossAttackPattern
{
    void Enter(BossController controller);
    bool Update(BossController controller); // true 반환 시 공격 종료
    void Exit(BossController controller);
}
```

### 2.2. 컨텍스트의 역할 (BossAttackState)

이제 FSM의 `BossAttackState`는 어떠한 공격이 수행되는지 알 필요가 없다. 단지 주입된 전략 객체(Strategy)를 실행할 뿐이다.

```csharp
public class BossAttackState : BossBaseState
{
    private IBossAttackPattern _currentPattern;

    public void SetPattern(IBossAttackPattern pattern) => _currentPattern = pattern;

    public override void Enter() => _currentPattern?.Enter(Controller);
    public override void Update()
    {
        if (_currentPattern != null && _currentPattern.Update(Controller))
        {
            // 패턴 종료 신호를 받으면 다시 전투 대기 상태로 전환
            Controller.StateMachine.ChangeState(Controller.CombatState);
        }
    }
}
```

## 3. Result: 유연한 확장성 확보

이러한 설계의 도입으로 얻은 이점은 명확하다.

1.  **결합도 감소**: `BossAttackState`는 구체적인 공격 내용(애니메이션 이름, 데미지 타이밍 등)으로부터 완전히 독립된다.
2.  **확장성 (OCP 준수)**: 새로운 패턴인 `ChargeAttackPattern`이 필요하다면, 기존 코드를 한 줄도 건드리지 않고 `IBossAttackPattern`을 구현하는 새 클래스만 추가하면 된다.
3.  **코드 선언성**: 보스의 공격 로직이 `Attacks/` 폴더 내에 개 별 클래스로 관리되므로 코드 가독성과 탐색 속도가 비약적으로 향상된다.

결국 좋은 설계란 "코드를 짤 때의 편함"보다 "코드를 수정할 때의 안도감"을 주는 구조임을 다시금 체감한다.
