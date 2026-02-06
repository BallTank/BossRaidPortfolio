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
