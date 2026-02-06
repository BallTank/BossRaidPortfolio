# [Portfolio] Boss Raid Portfolio: 확장성과 최적화를 고려한 기술 아키텍처 설계

## 1. 개요 (Vision: Capsule & Cube)

본 프로젝트는 화려한 에셋에 의존하기보다 **'견고한 설계'**와 **'네트워크 확장성'**을 증명하는 데 목적을 둡니다. 모든 캐릭터와 보스는 캡슐과 큐브로 대체하여 로직의 순수성을 높였으며, 향후 **P2P 멀티플레이어(Netcode for GameObjects)** 전환을 전제로 한 **'데이터 중심 설계'**를 채택했습니다.

---

## 2. Decoupled Input System: '누가'와 '무엇을'의 철저한 분리

단순히 `Input.GetKey`를 `Update`에서 호출하는 방식은 멀티플레이어 환경에서 재사용이 불가능합니다. 이를 해결하기 위해 **Input Provider 패턴**을 도입했습니다.

* 
**구조체 기반 데이터 패킹 (Zero-GC):** `PlayerInputPacket`을 `struct`로 설계하여 가비지 컬렉션 발생을 원천 차단했습니다.


* 
**비트 마스킹(Bit-Packing) 최적화:** 8개의 `bool` 변수 대신 1바이트(`byte`) 내에 `InputFlag`를 담아 네트워크 패킷 크기를 최소화할 수 있는 기반을 마련했습니다.


* 
**추상화 (IInputProvider):** 로직(Controller)은 인터페이스만 바라봅니다. 이는 입력 소스를 로컬 키보드에서 네트워크 패킷으로 단 한 줄의 코드 수정 없이 교체할 수 있는 유연성을 제공합니다.



---

## 3. Character FSM: 상태 패턴을 활용한 책임 분산

`PlayerController`가 비대해지는 'God Class' 현상을 막기 위해 **Class-based State Pattern**을 적용했습니다.

* 
**상태별 책임 격리:** `Idle`, `Move`, `Dash`, `Attack` 등 각 상태를 독립된 클래스로 분리하여 전이 로직의 명확성을 확보했습니다.


* 
**인터페이스 기반 상호작용 (IDamageable):** 보스와 플레이어는 `IDamageable`을 공유합니다. 공격자는 상대가 누구인지 알 필요 없이 데미지만 전달하며, 이는 객체 간 결합도를 낮추는 핵심 설계입니다.



---

## 4. Combat Logic & Physics: Zero-Allocation 전략

매 프레임 발생하는 물리 연산과 객체 생성은 런타임 성능의 적입니다. 본 프로젝트는 **'Zero-Allocation Architecture'**를 지향합니다.

* 
**Non-Alloc API 활용:** `OverlapSphere` 대신 `OverlapSphereNonAlloc`을 사용하여 가비지(GC) 발생 없는 정교한 프레임 단위 판정을 구현했습니다.


* 
**Generic Object Pooling:** 보스의 대규모 탄막(Barrage) 패턴 시 `Instantiate/Destroy`를 배제하고, `Stack<T>` 기반의 풀링 시스템을 통해 메모리 파편화를 방지했습니다.



---

## 5. Technical Insight: 면접관을 위한 'Deep Dive'

이 프로젝트의 진정한 가치는 **'엔진의 내부 동작 원리 이해'**에 있습니다.

| 기술 포인트 | 적용 내용 | 기대 효과 |
| --- | --- | --- |
| **Bitwise Ops** | 1Byte 내 버튼 상태 패킹 

 | 네트워크 대역폭 최적화 

 |
| **Struct Data** | 입력 데이터를 클래스가 아닌 구조체로 전달 

 | 힙 메모리 할당 감소 및 GC 스파이크 방지 

 |
| **Authority** | 호스트 권한 기반의 AI 패턴 제어 

 | 멀티플레이 환경에서의 동기화 무결성 확보 

 |

---

## 6. 결론 및 향후 계획

처음엔 코드가 복잡해 보일 수 있으나, 이는 **유지보수와 확장성**을 고려한 고도의 설계입니다. 객체 간의 책임을 명확히 분산하고 인터페이스를 적극 활용함으로써, 새로운 기능이 추가되어도 기존 시스템의 안정성을 유지할 수 있습니다.

**Next Steps:**

* 
`CharacterController` 기반의 물리 이동 로직 고도화 


* 보스 AI 패턴(The Cube)의 FSM 구현 및 최적화 