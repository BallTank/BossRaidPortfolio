# [Portfolio] 전투 HUD 이벤트 기반 연동: HP 갱신과 데미지 피드백 파이프라인

## 1. 작업 배경

`2026-02-23` 작업의 목표는 UI를 단순 배치에서 끝내지 않고, 전투 로직과 **일관된 이벤트 경로**로 묶는 것이었다.

- 전투 HUD 골격 배치
- `Health` 이벤트 기반 HP 바 갱신
- `DamageCaster` 공격 윈도우 결과를 HUD 피드백으로 연결
- `ShowHud(bool)`로 HUD 전체 표시 제어

핵심은 "UI가 데이터를 폴링해서 읽는 구조"가 아니라, "전투 시스템이 이벤트를 발행하고 UI가 이를 반영하는 구조"로 바꾼 점이다.

---

## 2. 왜 이벤트 기반으로 작성했는가

UI를 `Update()`에서 매 프레임 갱신하는 방식은 처음엔 단순하지만, 전투 기능이 늘어날수록 문제가 커진다.

1. 상태 변화가 없어도 매 프레임 조건문/참조 체크를 반복한다.
2. 피격, 사망, 콤보 종료처럼 "순간 이벤트"를 타이밍 맞춰 동기화하기 어렵다.
3. HUD 로직이 전투 로직 내부로 스며들어 책임이 섞인다.

이번 구현에서 이벤트를 채택한 이유는 "변화가 발생한 순간에만 반응"하도록 만들기 위해서다.  
즉, `Health`와 `DamageCaster`는 사실(데이터 변화)을 알리고, `CombatHUDController`는 표시(뷰)만 담당한다.

### 이벤트 방식의 장점

| 항목 | 폴링 기반 UI | 이벤트 기반 UI |
| --- | --- | --- |
| 갱신 시점 | 매 프레임 검사 | 변화가 생긴 순간에만 갱신 |
| 성능/비용 | 불필요한 반복 체크 발생 | 필요할 때만 실행 |
| 동기화 정확도 | 프레임 의존으로 엇갈릴 수 있음 | 피격/사망/공격종료 시점과 직접 연결 |
| 결합도 | UI가 전투 상태를 계속 조회 | 전투는 이벤트 발행, UI는 구독만 수행 |
| 확장성 | 기능 추가 시 Update 분기 증가 | 구독자 추가로 확장 가능 |

---

## 3. HP 바 갱신: Health -> CombatHUDController

### 코드 1) Health 이벤트 발행

```csharp
// Health.cs
public event Action<int> OnDamageTaken;
public event Action OnDeath;

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

`Health`는 UI를 직접 모르고 이벤트만 발행한다. 이게 결합도를 낮추는 핵심이다.

### 코드 2) HUD 초기화 + 이벤트 구독

```csharp
// CombatHUDController.cs
public void Initialize(Health playerHealth, Health bossHealth)
{
    UnbindHealthEvents();

    _playerHealth = playerHealth;
    _bossHealth = bossHealth;

    BindHealthEvents();
    RefreshAllHealthBars();
}

private void BindHealthEvents()
{
    if (_playerHealth != null)
    {
        _playerHealth.OnDamageTaken += HandlePlayerDamaged;
        _playerHealth.OnDeath += HandlePlayerDied;
    }

    if (_bossHealth != null)
    {
        _bossHealth.OnDamageTaken += HandleBossDamaged;
        _bossHealth.OnDeath += HandleBossDied;
    }
}
```

HUD는 `Initialize`에서 참조를 주입받고, 이벤트를 구독해 HP 바를 즉시 갱신한다.

### 코드 3) 수치 텍스트 대신 Fill 비율 갱신

```csharp
// CombatHUDController.cs
private void RefreshPlayerHealthBar()
{
    if (_playerHealth == null) return;
    SetPlayerHpNormalized(_playerHealth.HealthRatio, _playerHealth.CurrentHealth, _playerHealth.MaxHealth);
}

public void SetPlayerHpNormalized(float ratio, int current, int max)
{
    _ = current;
    _ = max;

    float clampedRatio = Mathf.Clamp01(ratio);
    if (_playerHpFill != null)
    {
        _playerHpFill.fillAmount = clampedRatio;
    }
}
```

표시 경로를 `HealthRatio -> fillAmount`로 통일해, 이벤트가 오면 바로 시각 반영되도록 정리했다.

---

## 4. 데미지 피드백: DamageCaster -> PlayerController -> HUD

### 코드 4) 공격 윈도우 결과 이벤트 발행

```csharp
// DamageCaster.cs
public event Action<bool, int> OnAttackWindowResolved;

public void DisableHitbox()
{
    _isCasting = false;

    if (!_attackWindowOpen) return;

    bool isHit = _attackWindowTotalDamage > 0;
    OnAttackWindowResolved?.Invoke(isHit, _attackWindowTotalDamage);

    _attackWindowOpen = false;
    _attackWindowTotalDamage = 0;
}
```

공격이 끝나는 시점에 `적중 여부 + 누적 피해량`을 한 번만 발행한다.

### 코드 5) PlayerController에서 HUD 피드백 브리지

```csharp
// PlayerController.cs
private void Awake()
{
    if (_damageCaster != null)
    {
        _damageCaster.SetOwner(gameObject);
        _damageCaster.ForceDisableHitbox();
        _damageCaster.OnAttackWindowResolved += HandleAttackWindowResolved;
    }
}

private void HandleAttackWindowResolved(bool isHit, int totalDamage)
{
    _combatHUD?.ShowDamageFeedback(isHit, totalDamage);
}
```

판정 시스템과 UI 사이를 `PlayerController`가 중계한다.  
이 구조 덕분에 `DamageCaster`는 UI 의존 없이 재사용 가능하다.

### 코드 6) 고정형 피드백 표시

```csharp
// CombatHUDController.cs
public void ShowDamageFeedback(bool isHit, int totalDamage)
{
    if (_damageFeedbackText == null) return;

    if (!isHit)
    {
        HideDamageFeedbackImmediate();
        return;
    }

    _damageFeedbackText.text = $"HIT {Mathf.Max(0, totalDamage)}";
    _damageFeedbackText.gameObject.SetActive(true);
}
```

비적중일 때는 숨기고, 적중일 때만 고정 앵커 텍스트를 노출한다.

---

## 5. HUD 가시성 제어와 판정 안정화

### 코드 7) HUD 전체 토글

```csharp
// CombatHUDController.cs
public void ShowHud(bool visible)
{
    if (_playerTorsoImage != null) _playerTorsoImage.gameObject.SetActive(visible);
    if (_playerHpFill != null) _playerHpFill.gameObject.SetActive(visible);
    if (_playerNameText != null) _playerNameText.gameObject.SetActive(visible);
    if (_bossHpFill != null) _bossHpFill.gameObject.SetActive(visible);
    if (_bossNameText != null) _bossNameText.gameObject.SetActive(visible);

    if (!visible)
    {
        HideDamageFeedbackImmediate();
    }
}
```

연출/전투 상태에 따라 HUD를 통째로 숨길 수 있도록 제어 경로를 분리했다.

### 코드 8) 잔존 히트박스 가드

```csharp
// DamageCaster.cs
public void ForceDisableHitbox()
{
    ResetCastingState();
}
```

```csharp
// AttackState.cs
public override void Exit()
{
    Controller.OnHitEnd();
    _comboIndex = 0;
    _reserveNextCombo = false;
}
```

상태 전환 중 판정이 남아 "유령 타격"으로 이어지는 리스크를 줄이기 위한 안전장치다.

---

## 6. 정리

이번 UI 작업은 레이아웃 배치보다 **전투 이벤트 파이프라인 정리**에 가까웠다.  
왜 이렇게 썼는지 한 줄로 정리하면: **UI를 상태 조회기가 아니라, 도메인 이벤트를 소비하는 뷰로 만들기 위해서**다.

- `Health`는 이벤트를 발행
- `CombatHUDController`는 이벤트를 구독해 HP를 반영
- `DamageCaster`는 공격 윈도우 결과를 발행
- `PlayerController`는 그 결과를 HUD 피드백으로 중계

이벤트를 쓰면 얻는 실질적 이점은 분명하다.

- 변화가 있을 때만 갱신하므로 경로가 단순해진다.
- 피격/사망/타격 종료 시점과 UI 반영 시점이 정확히 맞는다.
- UI, 전투, 판정 시스템을 독립적으로 수정/테스트하기 쉬워진다.

즉, UI는 전투 로직의 부속물이 아니라, 이벤트 기반으로 동작하는 독립 뷰 레이어로 정리됐다.
