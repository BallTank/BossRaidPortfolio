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
