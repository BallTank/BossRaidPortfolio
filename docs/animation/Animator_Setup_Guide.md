# Animator Setup Guide (Logic/Visual Separation)

이 문서는 **로직(Player)과 비주얼(Visual Model)**을 철저히 분리하여 Animator를 설정하는 방법을 설명합니다.

**목표 구조:**
```
Player (Root)        <-- PlayerController (Logic), CharacterController (Physics)
 └── Visual (Child)  <-- Animator (Visual), Mesh Renderer
```

## 1. 계층 구조 설정 (Hierarchy Setup)

1. **Player 오브젝트 (최상위)**
   - `PlayerController` 스크립트가 붙어있는 오브젝트입니다.
   - **중요:** 이 오브젝트에는 `Animator` 컴포넌트가 **없어야** 합니다. 있다면 우클릭 -> Remove Component 하십시오.
   - `CharacterController`는 여기에 있어야 합니다 (물리 충돌 담당).
   - **[업데이트]** 코드 수정됨: 이제 `PlayerController` 스크립트가 자동으로 자식 오브젝트의 Animator를 찾습니다.
> 따라서 Inspector의 `Animator` 필드는 **비워두셔도 됩니다 (None)**. 게임 시작 시 `GetComponentInChildren`으로 자동 연결됩니다.

1. Hierarchy 창에서 **Player** 오브젝트 선택.
2. Inspector의 **Animator** 컴포넌트 확인.

2. **Visual 오브젝트 생성 (자식)**
   - Player 아래에 빈 오브젝트를 생성하거나, 캐릭터 모델(FBX)을 드래그하여 자식으로 넣으십시오.
   - 이름을 `Visual` 또는 `Model`로 변경하십시오.
   - **이 자식 오브젝트에 `Animator` 컴포넌트가 있어야 합니다.** (보통 모델을 넣으면 자동으로 달려있습니다).

## 2. Animator Controller 연결

1. **Animator 컴포넌트 찾기**
   - 방금 만든 **자식 오브젝트(Visual)**를 선택하십시오.
   - Inspector 창에서 `Animator` 컴포넌트를 확인하십시오.

2. **Controller 할당**
   - 프로젝트 창에서 `Assets/Animations/PlayerAnimator.controller` 파일을 찾으십시오.
   - 이 파일을 드래그하여, **자식 오브젝트의 Animator > Controller** 빈칸에 넣으십시오.
   - **Avatar**가 올바르게 할당되어 있는지 확인하십시오 (해당 모델의 Avatar).

## 3. 스크립트 연결 확인 (자동)

`PlayerController` 코드는 게임이 시작될 때(`Awake`) 자동으로 자식 오브젝트를 검색하여 `Animator`를 찾아냅니다.

```csharp
// 코드 내부 동작 (참고용)
_animator = GetComponentInChildren<Animator>(); // 자식들 중에서 Animator를 찾아서 연결함
```

따라서 Inspector의 `PlayerController` > `Animation` > `Animator` 필드는 **None (Empty)** 상태로 비워두시면 됩니다. 드래그해서 넣을 필요가 없습니다.

---

## 4. Animator 내부 상태 설정

(이전 가이드와 동일합니다. `PlayerAnimator.controller`를 더블 클릭하여 설정하십시오.)

### Parameters
- `Speed` (Float)

### States & Transitions
**주의: 이름이 틀리면 작동하지 않습니다 (대소문자 구별).**
Base Layer에 다음 상태들을 만드십시오.
1. **Locomotion** (Blend Tree: Idle <-> Run)
2. **Attack1**
3. **Attack2**
4. **Attack3**
5. **Quickshift_F**
6. **Jump**
   - Clip: `Assets/CombatGirlsCharacterPack/School_Katana_Girl/Animations/Normal/CrouchIdle.fbx` 내의 `CrouchIdle` (또는 원하는 점프 모션)
   - *참고: 현재 프로젝트에 명시적인 'Jump' 모션이 안 보인다면 `CrouchIdle`이나 `Evade`를 임시로 사용하십시오.*

*팁: 코드가 `CrossFade`로 애니메이션을 직접 제어하므로 복잡한 트랜지션 연결은 필요 없습니다.*

---

## 5. Boss (Dragon) Animator 설정

### 5.1. 계층 구조
```
Boss (Root)          <-- BossController (Logic), CharacterController (Physics)
 └── Dragon (Child)  <-- Animator (Visual), BossVisual.cs
```

### 5.2. Controller 구성

**파일**: `Assets/Animations/BossAnimator.controller` (또는 Dragon 프리팹 내장)

#### Parameters
| Name | Type | 용도 |
|------|------|------|
| `Speed` | Float | Locomotion Blend Tree (Idle ↔ Walk 전환) |

#### States
| State Name | Motion | 비고 |
|------------|--------|------|
| `Locomotion` | Blend Tree (Idle ↔ Walk) | Threshold `3.5` 기준 전환 |
| `Basic Attack` | BasicAttack clip | 근접 머리 휘두르기 |
| `Lunge Attack` | LungeAttack clip | 도약 돌진 패턴 |
| `Flame Attack` | `attackFlame.fbx` (`DragonUsurper`) | Projectile 패턴 진입 시 우선 재생 |
| `TakeOff` | `takeOff` (`DragonUsurper`) | AoE 패턴 이륙 시작 |
| `FlyForward` | `FlyForward` (`DragonUsurper`) | AoE 패턴 접근/이동 구간 |
| `FlyIdle` | `FlyIdle` (`DragonUsurper`) | AoE 브레스/장판 생성 중 체공 루프 |
| `Land` | `Land` (`DragonUsurper`) | AoE 종료 후 착지 |
| `Hit` | Hit clip | 피격 경직 |
| `Die` | Die clip | 사망 |

### 5.3. 코드 제어 방식 (`BossVisual.cs`)
```csharp
// CrossFade로 직접 전환 — Animator Transition 화살표 불필요
public void PlayIdle()       => CrossFade(AnimLocomotion);
public void PlayMove()       => CrossFade(AnimLocomotion);  // Speed 파라미터로 제어
public void PlayAttack()     => CrossFade(AnimBasicAttack);
public void PlayLungeAttack() => CrossFade(AnimLungeAttack);
public void PlayProjectileAttack() => CrossFade(AnimFlameAttack);
// AoE 체인(구현 완료)
public void PlayTakeOff()    => CrossFade(AnimTakeOff);
public void PlayFlyForward() => CrossFade(AnimFlyForward);
public void PlayFlyIdle()    => CrossFade(AnimFlyIdle);
public void PlayLand()       => CrossFade(AnimLand);
```

> **참고**: `PlayIdle()`과 `PlayMove()`는 둘 다 Locomotion Blend Tree를 사용하며, `SetSpeed()` 메서드로 `Speed` 파라미터를 조절하여 Idle/Walk을 전환합니다.
> **참고 2**: `PlayProjectileAttack()`는 `Flame Attack` 상태를 우선 탐색하고, 없으면 `Fireball Shoot` 또는 `Basic Attack`으로 폴백합니다.
> **참고 3**: `PlayTakeOff()/PlayFlyForward()/PlayFlyIdle()/PlayLand()`는 Pattern 4(AoE) 런타임에서 사용 중인 API입니다.

### 5.4. Pattern 4 (AoE) 비행/브레스 연출 구현 기준

#### 권장 시퀀스
1. `TakeOff`: 지상 패턴 종료 후 즉시 이륙.
2. `FlyForward`: 플레이어 쪽으로 전진하며 캐스팅 위치 확보.
3. `FlyIdle`: 체공 유지 + fire prefab 낙하 동기화 + 장판 생성.
4. `Land`: 장판 활성 구간 종료 후 착지.

#### Animator 운영 규칙
- AoE 체인 상태는 코드에서 `CrossFade`로 직접 제어합니다(복잡한 전이 화살표 최소화).
- `FlyIdle`은 루프 클립으로 설정하고, 패턴 종료 조건을 코드 타이머(`activeDuration`)로 관리합니다.
- AoE 캐스팅 단계는 `PlayFlyIdle()` 기준으로 유지하며, Pattern 3 오인 방지를 위해 AoE 코드에서 `PlayProjectileAttack()`를 강제 호출하지 않습니다.
- AoE 발사체는 기존 `BossProjectilePool`의 fire prefab을 재사용합니다(별도 AoE 전용 projectile/pool 미사용).
- fire prefab 착지 시점과 실제 공격 시작 시점을 동일하게 유지합니다(`telegraphDuration == impactTime`).
- 상태 미존재 시 `Locomotion`/`Idle`로 폴백해 멈춤 상태를 방지합니다.

