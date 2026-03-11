# [Portfolio] 동시 사망 판정: `Update` vs `LateUpdate`

## 1. 문제 정의

플레이어와 보스가 거의 동시에 사망하는 상황에서, 결과를 어떤 타이밍에 확정해야 일관성이 유지되는지 정리했다.

핵심은 다음 두 책임을 분리하는 것이다.

- 트리거(사망 유발): `Update`
- 판정(결과 확정): `LateUpdate`

---

## 2. 결론

현재 프로젝트 기준 권장 패턴은 한 줄로 정리된다.

**`Update`에서 동시에 죽이고, `LateUpdate`에서 결과를 확정한다.**

이렇게 분리하면 테스트 재현성과 실제 판정 일관성을 동시에 확보할 수 있다.

---

## 3. 왜 이렇게 분리하나

`GameManager`는 여러 오브젝트에서 올라온 사망 신호를 모아 최종 결과를 확정하는 책임을 가진다.  
그래서 프레임 후반(`LateUpdate`)에서 판정하는 구조가 적합하다.

반대로 테스트 트리거까지 `LateUpdate`에 두면 `GameManager.LateUpdate`와 실행 순서가 섞일 수 있다.  
이 경우 같은 입력에서도 판정 프레임이 밀리거나 흔들려, 테스트 재현성이 떨어질 수 있다.

즉, **`LateUpdate`끼리 섞이면 판정 타이밍 이상이 생길 수 있다**는 점이 핵심이다.

---

## 4. 현재 결과 판정 코드 (`GameManager`)

```csharp
// Assets/Scripts/Common/GameManager.cs
private void LateUpdate()
{
    if (CurrentState != GameFlowState.InGame) return;
    if (_isGameOverResolved) return;
    if (!_playerDead && !_bossDead) return;

    if (_bossDead)
    {
        ResolveGameOver(GameResult.Victory);
        return;
    }

    ResolveGameOver(GameResult.Defeated);
}

private void HandlePlayerDeath()
{
    _playerDead = true;
}

private void HandleBossDeath()
{
    _bossDead = true;
}
```

핵심은 `_bossDead`를 먼저 검사하는 tie-break 규칙이다.  
따라서 동시 사망이면 `Victory`가 나온다.

---

## 5. 사망 이벤트 발생 코드 (`Health`)

```csharp
// Assets/Scripts/Common/Combat/Health.cs
public void TakeDamage(int damage)
{
    if (IsDead || _isInvincible) return;
    if (damage <= 0) return;

    _currentHealth -= damage;
    OnDamageTaken?.Invoke(damage);

    if (_currentHealth <= 0)
    {
        Die();
    }
}

private void Die()
{
    _currentHealth = 0;
    OnDeath?.Invoke();
}
```

`OnDeath`가 올라오면 `GameManager`는 플래그만 세팅하고, 최종 결론은 `LateUpdate`에서 낸다.

---

## 6. 정리

- 동시 사망 판정의 안정성은 **역할 분리**에서 나온다.
- 트리거는 `Update`, 판정은 `LateUpdate`로 고정한다.
- 테스트 목적의 강제 동시 사망 검증에서도 이 규칙을 유지해야 프레임 단위 흔들림을 줄일 수 있다.

