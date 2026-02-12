# 🧠 OCP를 지키는 보스 패턴 설계: Strategy Pattern의 승리

> **"새로운 보스 공격 패턴 100개를 추가해도, 상태 머신(FSM) 코드는 단 한 줄도 수정할 필요가 없다면?"**

게임 개발, 특히 보스 AI를 만들다 보면 기획자들의 아이디어는 끝이 없다.
*"내려찍기 추가해주세요.", "브레스 쏘게 해주세요.", "반피 이하일 땐 3연격으로 바꿔주세요..."*

이때마다 `BossAttackState.cs`를 열어 `switch-case` 문을 늘리고 있다면, 이 글이 도움이 될 것이다. 오늘은 **Strategy Pattern(전략 패턴)**을 통해 **개방-폐쇄 원칙(OCP)**을 준수하며 유연한 보스 AI를 설계한 경험을 공유한다.

---

## 1. 문제: 비대해지는 `BossAttackState`

초기 구현에서는 `BossAttackState` 안에서 모든 공격 로직을 처리하려 했다.

```csharp
// ❌ Bad Code: 상태 클래스가 너무 많은 것을 알고 있음
public class BossAttackState : BossBaseState
{
    public override void Update()
    {
        if (type == AttackType.Basic)
        {
            // 기본 공격 로직...
        }
        else if (type == AttackType.Claw)
        {
            // 할퀴기 공격 로직... 이동도 해야 하고, 회전도 해야 하고...
        }
        // ... 패턴이 늘어날수록 이 파일은 1000줄, 2000줄이 넘어간다.
    }
}
```

이 방식의 문제는 명확하다.
1.  **SRP 위반**: 상태 관리가 목적인 클래스가 온갖 공격의 세부 로직까지 다 떠안는다.
2.  **OCP 위반**: 패턴 하나 추가할 때마다 기존 코드를 수정해야 한다. (실수로 다른 패턴 코드를 건드릴 위험!)

---

## 2. 해결: 전략 패턴 (Strategy Pattern) 도입

우리는 **"공격의 실행(What)"**과 **"공격의 관리(When/How)"**를 분리하기로 했다.

### 핵심 인터페이스: `IBossAttackPattern`

```csharp
public interface IBossAttackPattern
{
    // 공격 시작 시 (애니메이션 재생, 범위 표시 등)
    void Enter(BossController controller);

    // 프레임별 로직 (이동, 판정 등). false 반환 시 공격 종료.
    bool Update(BossController controller);

    // 공격 종료 및 정리
    void Exit(BossController controller);
}
```

이제 `BossAttackState`는 더 이상 공격이 "어떻게" 동작하는지 알 필요가 없다. 그저 **"현재 장착된 패턴을 실행하라"**는 명령만 내린다.

```csharp
// ✅ Good Code: 구체적인 로직을 모르는 상태
public class BossAttackState : BossBaseState
{
    private IBossAttackPattern _currentPattern;

    public void SetPattern(IBossAttackPattern pattern)
    {
        _currentPattern = pattern;
    }

    public override void Enter()
    {
        _currentPattern?.Enter(Controller);
    }

    public override void Update()
    {
        // 패턴이 끝났다고 하면(false), 그때 상태를 전환한다.
        if (!_currentPattern.Update(Controller))
        {
            Controller.ChangeState(Controller.IdleState);
        }
    }
}
```

---

## 3. 실전 적용: `ClawAttackPattern` 추가하기

오늘 새로운 기획 요청이 들어왔다.
> **"보스가 플레이어를 향해 회전하다가, 갑자기 앞으로 확 튀어나가면서 할퀴는 'Claw Attack'을 만들어달라."**

이 복잡한 로직을 추가하기 위해 수정된 파일은 **단 하나**다. (`ClawAttackPattern.cs`)

```csharp
public class ClawAttackPattern : IBossAttackPattern
{
    private float _timer;
    private ClawAttackSettings _settings;

    public void Enter(BossController controller)
    {
        // 1. 시각 효과 재생
        controller.Visual.PlayClawAttack();
        // 2. 공격 범위(Hitbox) 켜기 (데미지 1.5배)
        controller.DamageCaster.EnableHitbox(
            (int)(controller.AttackDamage * _settings.damageMultiplier)
        );
    }

    public bool Update(BossController controller)
    {
        _timer -= Time.deltaTime;

        // 돌진 타이밍에는 앞으로 이동
        if (_timer > _settings.rushDuration)
        {
            controller.MoveTo(controller.transform.forward, _settings.rushSpeed);
        }
        
        return _timer > 0; // 시간이 남았으면 계속 실행 (true)
    }
}
```

기존의 `BossAttackState.cs`나 `BasicAttackPattern.cs`는 **단 한 글자도 수정하지 않았다.** 이것이 바로 **OCP(확장에는 열려 있고, 수정에는 닫혀 있다)**의 힘이다.

---

## 4. 결론

전략 패턴을 적용함으로써 얻은 이점은 다음과 같다.

1.  **유지보수의 안전함**: 새 패턴을 짜다가 버그가 나도, 기존 패턴(`BasicAttack`)에는 전혀 영향을 주지 않는다.
2.  **협업의 용이함**: A개발자는 '브레스 패턴', B개발자는 '꼬리치기 패턴'을 동시에 작업해도 충돌(Merge Conflict)이 거의 발생하지 않는다.
3.  **테스트의 편리함**: 특정 패턴 클래스만 따로 떼어내어 단위 테스트하기 좋다.

**보스 패턴, 이제 '하드코딩'하지 말고 '전략'적으로 주입하자.**

---
*Next Topic: 이렇게 만든 화려한 드래곤, Mesh Collider 없이 어떻게 정교하게 피격 판정을 최적화했을까? (Compound Collider & NonAlloc)*
