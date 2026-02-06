# 📜 Coding Standard: Boss Raid Portfolio

이 문서는 프로젝트의 성능 최적화(Zero-GC), 유지보수성, 네트워크 확장성을 위한 코드 작성 규칙을 정의합니다.

## 1. Memory Management & Performance (Zero-GC)

* **No Allocation in Update**: `Update`, `FixedUpdate`, `LateUpdate` 등 매 프레임 호출되는 메서드 내에서 `new` 키워드 사용(객체 생성)을 엄격히 금지한다.
* **Struct over Class**: 네트워크 패킷이나 단순 데이터 전달자(DTO)는 스택 메모리를 사용하는 `struct`로 정의한다. (예: `PlayerInputPacket`)
* **Non-Alloc Physics API**: 물리 쿼리 시 반드시 `NonAlloc` 버전의 API를 사용한다.
* ❌ `Physics.OverlapSphere()`
* ✅ `Physics.OverlapSphereNonAlloc(position, radius, results)`


* **Collection Pre-allocation**: 물리 판정 결과나 객체 리스트를 담는 배열/리스트는 `Awake`에서 미리 최대 크기로 할당하여 재사용한다.

## 2. Architecture: Input & Logic Separation

* **Input Polling**: 로직 클래스는 직접 `Input.GetKeyDown`이나 `InputAction`에 접근하지 않는다. 반드시 `IInputProvider` 인터페이스를 통해 `PlayerInputPacket`을 전달받아야 한다.
* **State-Based Logic**: `PlayerController`의 `Update`는 오직 `StateMachine.Update()`를 호출하는 역할만 수행한다. 실제 이동, 점프, 공격 로직은 각 `BaseState` 상속 클래스 내에 구현한다.
* **Decoupled Components**: 컴포넌트 간 통신은 직접 참조보다 인터페이스(예: `IDamageable`)를 우선시하여 결합도를 낮춘다.

## 3. Network Optimization (Data-Oriented)

* **Bit-Masking**: 버튼 입력과 같은 불리언(bool) 데이터의 나열은 금지한다. `[Flags] enum`과 `byte` 또는 `int` 필드를 사용하여 비트 단위로 패킹한다.
* **Minimal Data Transfer**: 상태 동기화 시 전체 Transform을 보내기보다, 상태 인덱스와 입력값만 보내어 클라이언트에서 재현(Dead Reckoning 대비)할 수 있는 구조를 지향한다.

## 4. Physics & Rotation Standard

* **Camera-Relative Movement**: 이동 벡터 연산 시 반드시 카메라의 `forward`(Y값 제외)와 `right` 벡터를 기준으로 계산하여 직관적인 조작감을 제공한다.
* **Separated Rotation**:
* `CameraRoot`: 마우스 입력에 따라 즉각 회전.
* `Character Body`: 이동 입력이 있을 때만 이동 방향으로 부드럽게 회전(`Slerp`).



## 5. Naming & Style Conventions

* **Fields**: private 필드는 `_camelCase` 형식을 사용한다. (예: `_moveSpeed`)
* **Properties**: public 프로퍼티는 `PascalCase`를 사용한다. (예: `CurrentState`)
* **Methods**: 모든 메서드는 `PascalCase`를 사용하며, 동사로 시작한다.
* **Attributes**: 유니티 인스펙터 노출이 필요한 필드는 `[SerializeField]`를 명시한다.