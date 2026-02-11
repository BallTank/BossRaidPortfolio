# 📖 Technical Glossary: Boss Raid Portfolio

이 문서는 프로젝트 내에서 통용되는 주요 용어와 개념을 정의합니다.

## 1. Character Entities

* **The Capsule (플레이어)**: 플레이어가 조작하는 캐릭터 객체. 현재 그래픽이 캡슐 형태이므로 내부적으로 'Player' 또는 'TheCapsule'로 지칭한다.
* **The Cube (보스)**: 레이드 대상인 AI 보스 객체. 정육면체 형태이며, 'Boss' 또는 'TheCube'로 지칭한다.
* **CameraRoot**: 플레이어 캐릭터 자식 객체로 존재하며, 카메라의 회전(Pitch, Yaw)의 중심축이 되는 Transform. 캐릭터의 몸통 회전과 독립적으로 동작한다.
* **BossVisual**: 보스(`The Cube`)의 애니메이션, UI, 이펙트 등 시각적 요소를 전담하는 컴포넌트. `BossController`의 로직과 분리되어 있다.

## 2. Input & Network

* **Input Packet (PlayerInputPacket)**: 매 프레임 발생하는 입력 데이터를 담은 구조체. `moveDir`, `lookYaw`, `lookPitch`, `buttons`를 포함한다.
* **Input Provider**: 입력을 생성하는 주체. `LocalInputProvider`(키보드/마우스)와 추후 구현될 `NetworkInputProvider`(RPC/데이터 패킷)로 나뉜다.
* **Bit-Packing (비트 패킹)**: 여러 개의 `bool` 버튼 상태를 1바이트(`byte`) 데이터로 묶어 네트워크 전송 효율을 극대화하는 기법.
* **Input Flag**: 비트 패킹 시 각 버튼의 자릿수를 지정하는 `Enum` (예: Dash, Attack).

## 3. Architecture & Logic

* **FSM (Finite State Machine)**: 캐릭터의 행동 상태(Idle, Move, Attack 등)를 관리하는 유한 상태 머신.
* **StateMachine**: `BaseState`를 교체하며 라이프사이클(`Enter`, `Update`, `Exit`)을 관리하는 핵심 제어기.
* **BaseState**: 모든 상태 클래스가 상속받는 추상 클래스. `PlayerController` 참조를 가지며 실제 로직을 수행한다.
* **State Delegate**: `PlayerController`가 자신의 로직 처리를 현재 활성화된 `BaseState` 객체에 넘겨주는 행위.
* **Namespace (네임스페이스)**: 코드를 논리적으로 그룹화하고 클래스 이름 충돌을 방지하는 주소 체계. (예: `BossRaid.Patterns`)
* **Character Motor**: 실제 `CharacterController.Move()`를 실행하여 물리적인 이동을 처리하는 로직부.
* **Edge-Triggering (엣지 트리거)**: 입력 신호가 변하는 순간(예: 버튼을 누르는 찰나)을 포착하여 한 번만 로직을 실행하는 기법.
* **Cooldown (쿨타임)**: 기술 재사용 대기시간. `Time.time`을 기준으로 다음 실행 가능 시간을 계산하여 관리함.
* **Coupling (결합도)**: 두 모듈 간의 의존 정도. `Strong Coupling`은 변경에 취약하고, `Weak Coupling`은 인터페이스 등을 통해 유연하다.
* **Dependency Injection (의존성 주입)**: 객체가 의존하는 다른 객체를 직접 생성(`new`)하지 않고 외부에서 주입받아 결합도를 낮추는 패턴.
* **Visual Separation (비주얼 분리)**: 핵심 로직(`Controller`)과 시각적 표현(`Visual`)을 서로 다른 클래스로 분리하여, 로직 변경이 리소스(애니메이션 등)에 영향을 주지 않도록 하는 설계 패턴.
* **Magic String (매직 스트링)**: 코드 내에 직접 하드코딩된 문자열 리터럴. 오타 위험이 크므로 `const` 상수로 관리해야 함.
* **Coroutine Cleanup**: `StopAllCoroutines()`를 호출하여 진행 중인 비동기 작업(무적 타이머 등)을 강제 중단하는 기법. 상태 전환(사망 등) 시 잔존 코루틴이 예상치 못한 부작용을 일으키는 것을 방지.
* **Generic State Machine (제네릭 상태 머신)**: `StateMachine<T>` 형태로 구현하여, 플레이어와 보스가 동일한 상태 관리 로직을 공유하면서도 각자의 상태 타입(`PlayerState` vs `BossState`)을 안전하게 사용할 수 있게 하는 기법.
* **Feature Toggle (기능 토글)**: 개발 중인 기능이나 특정 로직(예: 보스 추적, 회전)을 인스펙터 체크박스 하나로 켜고 끌 수 있게 하여, 테스트 효율을 높이고 버그 추적을 용이하게 하는 개발 패턴.

## 4. Optimization (Performance)

* **Zero-GC (제로 GC)**: 런타임 중에 가비지 컬렉터가 작동하지 않도록 힙(Heap) 메모리 할당을 0에 가깝게 유지하는 설계 원칙.
* **Non-Alloc API**: 유니티 엔진 기능 중 결과값을 새로운 배열로 생성(`new`)하지 않고, 미리 할당된 배열에 채워 넣어주는 API.
    *   **VS Alloc (`Physics.OverlapSphere`)**: 호출할 때마다 매번 `Collider[]` 배열을 새로 생성(Allocation)하여 힙 메모리를 사용함. 프레임마다 호출하면 GC Spaike(랙)의 주범이 됨.
    *   **VS NonAlloc (`Physics.OverlapSphereNonAlloc`)**: 미리 만들어둔 배열(`pre-allocated array`)을 재사용함. 메모리 할당이 전혀 발생하지 않음(Garbage Free). 단, 배열 크기(`_maxTargets`) 이상의 충돌체는 감지하지 못하므로 크기 설정에 주의 필요.
* **Object Pooling**: 투사체나 이펙트를 파괴(Destroy)하지 않고 비활성화 후 재사용하여 CPU 부하를 줄이는 관리 방식.
* **Compound Collider (복합 충돌체)**: 하나의 무거운 Mesh Collider 대신, 여러 개의 가벼운 Primitive Collider(Box, Sphere, Capsule)를 조합하여 복잡한 형태의 충돌을 효율적으로 처리하는 기법. 보스의 부위별 피격 판정에 사용됨.

## 5. Combat System

* **Hitbox (히트박스)**: 공격 판정이 발생하는 가상의 구체 또는 박스 영역.
* **Hurtbox (허트박스)**: 피격 판정이 발생하는 영역. 캐릭터의 충돌체와 일치하거나 약간 작게 설정함.
* **Frame-based Detection**: 애니메이션의 특정 프레임 혹은 짧은 시간 동안만 물리 체크를 활성화하여 판정하는 방식.
* **Input Buffer (선입력)**: 애니메이션 종료 직전에 입력된 명령을 저장해두었다가, 동작 가능 시점에 즉시 실행하여 조작감을 향상시키는 시스템.
* **Animation Cancel (모션 캔슬)**: 현재 진행 중인 동작(특히 후딜레이)을 중단하고 대시 등의 긴급 회피 동작으로 즉시 전환하는 기법.
* **IDamageable**: 대상을 특정하지 않고 데미지 명령(`TakeDamage`)만 내릴 수 있게 해주는 추상화 인터페이스.
* **Animation Event Bridge**: 애니메이터의 타임라인 이벤트를 코드 로직(`PlayerController` 등)으로 연결해주는 중계 클래스.
* **IBossAttackPattern**: 보스 공격 패턴 인터페이스 (Strategy Pattern 적용). `Enter`/`Update`/`Exit` 메서드를 정의하여 `BossAttackState`가 구체 패턴을 몰라도 실행할 수 있게 함.
* **BasicAttackPattern**: `IBossAttackPattern`의 기본 구현체. 보스의 근접 공격(애니메이션 재생 + DamageCaster 활성화 + 타이머 기반 종료)을 측술화.
* **Invincibility Frame (무적 시간)**: 피격 후 일정 시간 동안 추가 데미지를 받지 않는 보호 기간. `Health.SetInvincible(true/false)`와 코루틴으로 관리.

## 6. Animation System

* **Animator Controller**: Unity의 애니메이션 상태 머신. FSM과 연동하여 상태 전환 시 애니메이션을 재생함.
* **CrossFade**: 현재 애니메이션에서 목표 애니메이션으로 부드럽게 블렌딩하는 Unity Animator 메서드. 끊김 없는 전환을 위해 사용.
* **Blend Tree**: 하나의 파라미터(예: `Speed`)에 따라 여러 애니메이션을 자동으로 섞어 재생하는 구조. Idle↔Run 전환에 사용.

## 7. Design Patterns

* **Strategy Pattern (전략 패턴)**: 알고리즘(행동)을 인터페이스로 추상화하여 런타임에 교체 가능하게 하는 디자인 패턴. 본 프로젝트에서는 `IBossAttackPattern`으로 보스 공격 패턴을 교체 가능하게 구현함.