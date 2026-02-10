# Unity 비트 연산 및 Null-Safe 패턴 분석

## 1. LayerMask 비트 연산 이해하기

보스 AI의 시야 체크(Line of Sight) 로직에서 다음과 같은 비트 연산이 사용된다:

```csharp
if (((1 << hit.collider.gameObject.layer) & obstacleMask) != 0)
{
    return false; // 장애물에 막힘
}
```

### 코드 분석

| 단계 | 표현식 | 설명 |
|------|--------|------|
| 1 | `hit.collider.gameObject.layer` | 충돌체의 레이어 인덱스 (0~31) |
| 2 | `1 << layer` | 레이어 인덱스를 비트마스크로 변환 |
| 3 | `& obstacleMask` | 장애물 레이어와 AND 연산 |
| 4 | `!= 0` | 교집합이 존재하면 true |

### 비트 시프트 예시

```
layer = 0  →  1 << 0  = 0000 0001 (십진수: 1)
layer = 6  →  1 << 6  = 0100 0000 (십진수: 64)
layer = 8  →  1 << 8  = 1 0000 0000 (십진수: 256)
```

### AND 연산 로직

```
충돌 레이어가 Obstacle(8)일 때:
     1 << 8 = 0000 0001 0000 0000
obstacleMask = 0000 0011 0000 0000  (Obstacle + Wall)
─────────────────────────────────
    AND 결과 = 0000 0001 0000 0000 (≠ 0) ✅ 장애물!

충돌 레이어가 Player(6)일 때:
     1 << 6 = 0000 0000 0100 0000
obstacleMask = 0000 0011 0000 0000
─────────────────────────────────
    AND 결과 = 0000 0000 0000 0000 (= 0) ❌ 장애물 아님
```

### 실무적 의미

> **"Raycast에 맞은 오브젝트의 레이어가 `obstacleMask`에 포함되어 있는가?"**

즉, 보스가 플레이어를 향해 Raycast를 쏠 때, 중간에 벽이나 장애물이 먼저 맞으면 **시야가 차단된 것**으로 판정한다.

---

## 2. Null-Conditional 연산자로 NullReferenceException 방지

### 문제 상황

```
NullReferenceException: Object reference not set to an instance of an object
BossRaid.Boss.BossIdleState.Enter () (at Assets/Scripts/Boss/BossFSM.cs:15)
```

보스 애니메이터(`BossVisual`)가 아직 생성되지 않은 상태에서 `Enter()` 메서드가 호출되어 발생.

### 원인 분석

```csharp
// BossController.cs
[SerializeField] private BossVisual animator;  // 인스펙터에서 미할당!
public BossVisual Visual => animator;

// BossFSM.cs - BossIdleState
public override void Enter()
{
    Controller.Visual.SetSpeed(0f);  // Visual이 null → 💥 NullReferenceException
}
```

### 해결 방법: Null-Conditional Operator (`?.`)

```csharp
// 변경 전
Controller.Visual.SetSpeed(0f);

// 변경 후
Controller.Visual?.SetSpeed(0f);  // Visual이 null이면 호출 건너뜀
```

### 적용 범위

| State | 메서드 | 수정 내용 |
|-------|--------|-----------|
| `BossIdleState` | `Enter()` | `Controller.StopMoving()` 사용 (이미 null-safe) |
| `BossSearchingState` | `Enter()` | `Visual?.SetSearchingUI(true)` |
| `BossSearchingState` | `Exit()` | `Visual?.SetSearchingUI(false)` |
| `BossHitState` | `Enter()` | `Visual?.TriggerHit()` |
| `BossDeadState` | `Enter()` | `Visual?.TriggerDie()` |

### 설계 원칙

1. **Defensive Programming**: 외부 의존성(Visual, Animator)이 없어도 핵심 로직은 동작해야 함.
2. **Graceful Degradation**: 비주얼이 없으면 애니메이션 없이 로직만 진행.
3. **일관성**: `BossController`의 `MoveTo()`, `StopMoving()`에서 이미 `if (animator)` 체크 사용 중.

---

## 3. 핵심 포인트

| 주제 | 기술 포인트 |
|------|-------------|
| **비트 연산** | `LayerMask`는 32비트 정수. 레이어 인덱스를 비트 위치로 변환하여 효율적인 집합 연산 가능. |
| **Null 안전성** | `?.` 연산자로 런타임 크래시 방지. 선택적 컴포넌트(Visual)는 항상 null 체크 필수. |
| **물리 최적화** | 비트 마스킹으로 특정 레이어만 필터링하여 불필요한 충돌 검사 최소화. |
