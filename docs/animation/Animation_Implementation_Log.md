# Animation Implementation Walkthrough

이 문서는 이번 대화에서 진행한 애니메이션 구현 작업을 정리합니다.

---

## 1. 최종 아키텍처 (Logic/Visual Separation)

```
Player (Root)
├── PlayerController.cs (로직)
├── CharacterController (물리)
└── Visual (Child)
    ├── Animator (비주얼 제어)
    └── School_Katana_FullBody (3D Mesh)
```

**핵심 원리**: 로직(물리)과 비주얼(렌더링)을 분리하여, 코드는 Parent를 움직이고, 자식 모델은 그에 따라 따라다님.

---

## 2. 코드 로직

### 2.1 애니메이션 상수 정의 ([PlayerController.cs](file:///d:/Unity-projects/BossRaidPortfolio/Assets/Scripts/PlayerController.cs#L30-L37))
```csharp
public const string ANIM_PARAM_SPEED = "Speed";
public const string ANIM_STATE_LOCOMOTION = "Locomotion";
public const string ANIM_STATE_DASH = "Quickshift_F";
public const string ANIM_STATE_ATTACK1 = "Attack1";
public const string ANIM_STATE_ATTACK2 = "Attack2";
public const string ANIM_STATE_ATTACK3 = "Attack3";
public const string ANIM_STATE_JUMP = "Jump";
```
*이유*: 문자열 오타 방지 및 중앙 집중 관리.

### 2.2 State별 애니메이션 호출
| State | 메서드 | 호출 코드 |
|-------|--------|-----------|
| [MoveState](file:///d:/Unity-projects/BossRaidPortfolio/Assets/Scripts/Player/States/MoveState.cs) | `Enter()` | `CrossFade(ANIM_STATE_LOCOMOTION)` |
| [MoveState](file:///d:/Unity-projects/BossRaidPortfolio/Assets/Scripts/Player/States/MoveState.cs) | `Update()` | `SetFloat(ANIM_PARAM_SPEED, magnitude)` |
| [DashState](file:///d:/Unity-projects/BossRaidPortfolio/Assets/Scripts/Player/States/DashState.cs) | `Enter()` | `CrossFade(ANIM_STATE_DASH)` |
| [AttackState](file:///d:/Unity-projects/BossRaidPortfolio/Assets/Scripts/Player/States/AttackState.cs) | `StartComboStep()` | `CrossFade(ANIM_STATE_ATTACK1/2/3)` |
| [JumpState](file:///d:/Unity-projects/BossRaidPortfolio/Assets/Scripts/Player/States/JumpState.cs) | `Enter()` | `CrossFade(ANIM_STATE_JUMP)` |

**`CrossFade` 사용 이유**: Animator Transition 화살표 없이 코드에서 직접 상태 전환 (FSM과 Animator 이중 관리 방지).

---

## 3. Animator Controller 설정

**파일**: `Assets/Animations/PlayerAnimator.controller`

### Parameters
| Name | Type | 용도 |
|------|------|------|
| `Speed` | Float | Locomotion Blend Tree |

### States
| State Name | Motion Clip |
|------------|-------------|
| `Locomotion` | Blend Tree (Idle ↔ Run) |
| `Attack1` | Attack1.fbx |
| `Attack2` | Attack2.fbx |
| `Attack3` | Attack3.fbx |
| `Quickshift_F` | Quickshift_F.fbx |
| `Jump` | CrouchIdle.fbx (임시) |

*참고*: Transition은 사용하지 않음. 코드에서 `CrossFade`로 직접 제어.

---

## 4. 발생한 에러 및 해결

### 4.1 캐릭터가 보이지 않음 (캡슐만 보임)
| 원인 | 해결 |
|------|------|
| Visual 오브젝트에 3D Mesh가 없었음 (빈 오브젝트) | FBX 모델을 Visual 자식으로 추가 |
| Player에 Mesh Filter/Renderer가 남아있어 덮어씀 | Player의 Mesh 컴포넌트 삭제 |

### 4.2 Attack에서 Idle로 복귀 안 됨
| 원인 | 해결 |
|------|------|
| `MoveState.Enter()`가 Locomotion으로 CrossFade하지 않음 | `CrossFade("Locomotion")` 추가 |

### 4.3 Unity Editor Inspector 에러
```
NullReferenceException: UnityEditor.GameObjectInspector...
```
| 원인 | 해결 |
|------|------|
| 스크립트 변경 중 에디터 캐시 오류 | Unity 재시작 |

### 4.4 중복 상수 정의 에러
```
The type 'PlayerController' already contains a definition for 'ANIM_STATE_ATTACK2'
```
| 원인 | 해결 |
|------|------|
| 편집 중 상수가 중복 삽입됨 | 중복 라인 삭제 |

---

## 5. 최종 결과
- ✅ Idle/Run Locomotion Blend Tree 작동
- ✅ Attack1 → Attack2 → Attack3 콤보 전환
- ✅ Quickshift_F 대시 애니메이션
- ✅ Jump 애니메이션 (임시 모션)
- ✅ 모든 State에서 Locomotion으로 정상 복귀

---

## 6. Boss (Dragon) 애니메이션 구현 (2025-02-11)

### 6.1. 아키텍처
```
Boss (Root)
├── BossController.cs (AI 로직)
├── CharacterController (물리)
└── Dragon (Child)
    ├── BossVisual.cs (애니메이션/이펙트)
    ├── Animator
    └── Dragon Mesh
```

**핵심 원리**: Player와 동일하게 Logic/Visual 분리. `BossController`는 `BossVisual`에 애니메이션 호출을 위임.

### 6.2. 애니메이션 상수 정의 (`BossVisual.cs`)
```csharp
private const string ANIM_LOCOMOTION   = "Locomotion";
private const string ANIM_BASIC_ATTACK = "Basic Attack";
private const string ANIM_LUNGE_ATTACK = "Lunge Attack";
private const string ANIM_FLAME_ATTACK = "Flame Attack";
// Hit, Die는 BaseVisual에서 상속
```

### 6.3. State별 애니메이션 호출
| 상태 | 메서드 | 호출 코드 |
|------|--------|-----------|
| Idle/Combat (이동) | `PlayMove()` / `SetSpeed()` | Blend Tree의 `Speed` 파라미터 조절 |
| Basic Attack | `PlayAttack()` | `CrossFade("Basic Attack")` |
| Lunge Attack | `PlayLungeAttack()` | `CrossFade("Lunge Attack")` (없으면 `"Claw Attack"` 폴백) |
| Projectile Attack | `PlayProjectileAttack()` | `CrossFade("Flame Attack")` 우선 (없으면 `"Fireball Shoot"` 폴백) |
| AoE TakeOff | `PlayTakeOff()` | `CrossFade("TakeOff")` |
| AoE Flight Loop | `PlayFlyForward()` / `PlayFlyIdle()` | `CrossFade("FlyForward/FlyIdle")` |
| AoE Land | `PlayLand()` | `CrossFade("Land")` |
| Hit | `TriggerHit()` | `CrossFade("Hit")` + Flash 이펙트 |
| Dead | `TriggerDie()` | `CrossFade("Die")` |

### 6.4. Blend Tree 설정
| 파라미터 | 값 범위 | 동작 |
|----------|---------|------|
| `Speed` = 0 | Idle | 정지 애니메이션 |
| `Speed` ≥ 3.5 | Walk | 걷기 애니메이션 |

> Threshold `3.5`는 `BossController.moveSpeed`와 동기화되어 있습니다.

### 6.5. Projectile 연출/탄도 동기화 (2026-02-19)
| 항목 | 구현 |
|------|------|
| 발사 시작 높이 | SpawnPoint의 Y를 그대로 사용 |
| 비행 중 높이 | `verticalFollowSpeed`로 `target.y`에 `MoveTowards` 수렴 |
| 수평 유도 | XZ 평면에서 `RotateTowards` 적용 (`homingStrength`, `homingDuration`) |
| 충돌 처리 안정성 | `OnTriggerEnter` + `OnCollisionEnter` 동시 지원, 부모 `IDamageable` 폴백 |

### 6.6. Pattern 4 (AoE) 연출 구현 로그 (2026-02-20)
| 단계 | 애니메이션 | 로직 트리거 |
|------|------------|-------------|
| Phase A | `TakeOff` | AoE 패턴 진입 |
| Phase B | `FlyForward` | 장판 생성 시작 위치 확보 |
| Phase C | `FlyIdle` + fire prefab 낙하 | 다중 장판 텔레그래프 생성/진행 |
| Phase D | `Land` | 장판 활성 구간 종료 후 지상 복귀 |

- 장판 텔레그래프는 중심→외곽 Fill(저알파 Red)로 통일.
- 장판 판정은 `tickInterval` 기반으로 처리하고, 애니메이션 재생 길이와 직접 결합하지 않습니다.
- AoE fire 연출은 `BossProjectilePool`의 기존 fire prefab을 재사용합니다.
- 플레이어 공격 시점은 fire prefab 착지 시점과 동기화합니다(`telegraphDuration == impactTime`).
- 장판 분포는 타겟 진행 방향 예측(`headingLeadTime`, `headingBias` 등)으로 전방 편향되어, 플레이어 진행 경로를 더 강하게 압박합니다.
- 런타임 폴백 디스크는 `fallbackYOffset = 0`으로 고정해 바닥 밀착을 유지합니다.

### 6.7. Projectile(Flame) 종료 복귀 안정화 (2026-02-20)
| 항목 | 구현 |
|------|------|
| 조기 복귀 방지 | 마지막 발사 후 즉시 Combat으로 복귀하지 않고 `postFireRecoveryDuration`만큼 대기 |
| 애니메이션 종료 동기화 | `AnimatorStateInfo.normalizedTime`가 `exitNormalizedTime` 이상일 때 상태 종료 허용 |
| 대상 상태 | `Flame Attack`, `Fireball Shoot`, `Basic Attack` 상태를 모두 종료 판정 대상으로 취급 |

- `volleyCount`만으로 종료를 판단하던 기존 방식에서, 최소 대기 + normalizedTime 조건 결합 방식으로 변경했습니다.
- Flame Attack 이후 Locomotion으로 급복귀하며 발생하던 시각적 튐(지터)을 완화합니다.

### 6.8. AoE 비행 연출 보호(Locomotion 잠금) (2026-02-20)
| 항목 | 구현 |
|------|------|
| 지상 이동 애니메이션 오염 방지 | `AoEAttackPattern.Enter()`에서 `SetLocomotionVisualSuppressed(true)` 적용 |
| 종료 시 복구 | `AoEAttackPattern.Exit()`에서 `SetLocomotionVisualSuppressed(false)` 해제 |
| FlyForward 폴백 보강 | `BossVisual.PlayFlyForward()`에서 비행 상태 미존재 시 `PlayFlyIdle()` 폴백 |

- 공중 연출(`TakeOff -> FlyForward -> FlyIdle -> Land`) 중 `MoveTo`/`StopMoving`가 Walk/Idle을 덮어쓰지 않도록 시각 보호 계층을 적용했습니다.
- Animator 구성 편차(FlyForward 미구성)에서도 Walk 혼입 대신 비행 루프를 유지해 연출 안정성을 확보했습니다.

