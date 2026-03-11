# [Portfolio] Lunge 뒤돌아감 버그 수정: 루트모션 브리지 적용기

## 1. 문제 정의

보스 `Lunge` 공격에서 아래 현상이 반복됐다.

- 도약 시작 프레임에서 플레이어를 향하지 않고 뒤돌아가거나 옆으로 새는 방향 오차
- 자식 `Visual`은 전진했는데 부모 `BossController` 루트 이동이 덜 반영되는 좌표 불일치

결과적으로 착지 위치, 타격 판정, 화면 연출이 서로 어긋나 전투 체감 품질이 떨어졌다.

관련 파일:

- Assets/Scripts/Boss/Attacks/LungeAttackPattern.cs
- Assets/Scripts/Boss/BossController.cs
- Assets/Scripts/Boss/BossVisual.cs

---

## 2. 원인 분석

### 2.1. 이동 소스가 분리됨

기존 Lunge는 패턴 코드의 수동 이동(`MoveRaw`)과 애니메이션 루트모션이 섞여 있었다.  
이 경우 애니메이션 전진량과 실제 전투 좌표가 쉽게 분리된다.

### 2.2. 부모/자식 좌표계 전달 부재

Animator가 자식 `Visual`에 붙어 있는 구조라, `OnAnimatorMove` 델타를 부모 루트로 전달하지 않으면  
"보이는 드래곤"과 "판정 기준 루트"가 다른 위치를 가질 수 있다.

### 2.3. 시작 프레임 방향 고정 부재

도약 시작 순간 방향이 잠기지 않으면 상태 전환/회전 보간 타이밍에 따라 첫 이동 벡터가 흔들린다.

---

## 3. 해결 전략

1. Lunge 시작 시 즉시 정렬 + 이동 방향 잠금
2. `OnAnimatorMove -> BossController.ApplyLungeRootMotion` 브리지 구성
3. `animator.deltaPosition`이 0/미소값일 때 `Visual` 월드 델타 폴백 적용
4. 히트박스 종료(`0.8`)와 상태 종료(`1.0`) 시점 분리

---

## 4. 핵심 코드 스니펫

### 4.1. Lunge 시작 시 방향 고정 + 루트모션 활성화

출처: Assets/Scripts/Boss/Attacks/LungeAttackPattern.cs

```csharp
public void Enter(BossController controller)
{
    controller.StopMoving();
    controller.ResetLungeRootMotionDebugLogWindow();

    if (controller.Target != null)
    {
        controller.RotateTowardsImmediate(controller.Target.position);
        controller.BeginLungeTravelDirectionLock(controller.Target.position);
    }
    else
    {
        controller.BeginLungeTravelDirectionLock(
            controller.transform.position + controller.transform.forward);
    }

    controller.Visual?.SetLungeRootMotionEnabled(true);
    controller.Visual?.PlayLungeAttack();
}
```

### 4.2. 루트모션 델타를 부모 루트 이동으로 적용

출처: Assets/Scripts/Boss/BossController.cs

```csharp
public void ApplyLungeRootMotion(Vector3 deltaPosition)
{
    if (_characterController == null) return;

    deltaPosition.y = 0f;
    float deltaMagnitude = deltaPosition.magnitude;
    if (deltaMagnitude <= LungeRootMotionMinStep) return;

    if (_isLungeTravelDirectionLocked)
    {
        _characterController.Move(_lungeTravelDirection * deltaMagnitude);
        return;
    }

    _characterController.Move(deltaPosition);
}
```

### 4.3. Animator 델타 우선, Visual 월드 델타 폴백

출처: Assets/Scripts/Boss/BossVisual.cs

```csharp
private void OnAnimatorMove()
{
    if (!_lungeRootMotionEnabled) return;
    if (_animator == null || _owner == null) return;

    Vector3 animatorDeltaPosition = _animator.deltaPosition;
    Vector3 appliedDeltaPosition =
        ResolveAppliedDeltaPosition(animatorDeltaPosition, out bool usedVisualFallback);

    _owner.ApplyLungeRootMotion(appliedDeltaPosition);
    RestoreVisualLocalPose();
    _previousVisualWorldPosition = transform.position;
}

private Vector3 ResolveAppliedDeltaPosition(
    Vector3 animatorDeltaPosition,
    out bool usedVisualFallback)
{
    usedVisualFallback = false;

    Vector3 animatorDeltaXZ = animatorDeltaPosition;
    animatorDeltaXZ.y = 0f;

    float epsilonSqr = RootMotionDeltaEpsilon * RootMotionDeltaEpsilon;
    if (animatorDeltaXZ.sqrMagnitude > epsilonSqr)
    {
        return animatorDeltaXZ;
    }

    Vector3 visualDelta = transform.position - _previousVisualWorldPosition;
    visualDelta.y = 0f;
    if (visualDelta.sqrMagnitude <= epsilonSqr)
    {
        return Vector3.zero;
    }

    usedVisualFallback = true;
    return visualDelta;
}
```

### 4.4. 판정 종료와 상태 종료 시점 분리

출처: Assets/Scripts/Boss/Attacks/LungeAttackPattern.cs

```csharp
private const float FixedHitboxOffPhaseRatio = 0.8f;
private const float FixedExitPhaseRatio = 1.0f;

public bool Update(BossController controller)
{
    AnimatorStateInfo stateInfo = controller.Visual.Animator.GetCurrentAnimatorStateInfo(0);
    float progress = stateInfo.normalizedTime;

    if (!_hitboxDisabled && progress >= FixedHitboxOffPhaseRatio)
    {
        controller.LungeDamageCaster?.DisableHitbox();
        _hitboxDisabled = true;
    }

    return progress >= FixedExitPhaseRatio;
}
```

---

## 5. 적용 결과

- Lunge 시작 시 뒤돌아감/옆샘 현상이 재현되지 않음
- 부모 루트와 자식 비주얼 좌표 불일치 해소
- 착지 지점이 애니메이션 이동과 전투 판정 기준에서 일관되게 수렴
- 연출 타이밍(상태 종료)과 밸런스 타이밍(히트박스 종료) 독립 제어 가능

---

## 6. 회고

이번 수정의 핵심은 "보이는 이동"과 "게임플레이 이동"을 분리하지 않는 것이다.  
Lunge 같은 돌진 패턴은 작은 좌표 오차도 즉시 버그 체감으로 연결된다.

정리하면 아래 4가지를 고정하면 안정성이 높다.

1. 시작 방향 1회 고정
2. 루트모션 브리지로 이동 소스 단일화
3. 델타 품질 편차(0/미소값) 폴백 경로 마련
4. 판정 종료와 상태 종료 타이밍 분리

동일 구조를 다른 돌진형 패턴에도 재사용할 계획이다.
