# 🔗 아키텍처 기술 로그: 의존성(Dependency)과 결합도(Coupling)

## 1. 개요
프로젝트 Boss Raid Portfolio의 개발 과정에서 핵심이 된 아키텍처 철학은 **"유연한 연결(Loose Coupling)"**이다. 본 문서는 개발 과정에서 논의된 **강한 의존성(Strong Dependency)**과 **약한 의존성(Weak Dependency)**의 개념을 정의하고, 실제 코드베이스(`PlayerController`, `FSM`)에 어떻게 적용되었는지 분석한다.

---

## 2. 의존성의 두 가지 얼굴

객체 지향 프로그래밍에서 A 클래스가 B 클래스를 사용할 때 '의존성'이 발생한다. 이 연결의 강도에 따라 시스템의 확장성이 결정된다.

### 2.1. 강한 의존성 (Strong Dependency)
*   **정의:** A 클래스가 B 클래스의 **구체적인 타입(Concrete Type)**을 직접 알고 있는 관계.
*   **특징:**
    *   `new` 키워드를 사용하여 직접 생성하거나 소유한다.
    *   B 클래스의 이름이나 생성자가 변경되면 A 클래스도 수정해야 한다.
    *   교체나 테스트가 어렵다.
*   **비유:** **심장(Heart)**. 이식 수술(코드 수정) 없이는 교체할 수 없는 필수 불가결한 관계.

### 2.2. 약한 의존성 (Weak Dependency)
*   **정의:** A 클래스가 B 클래스의 구체적인 정체를 모르고, **인터페이스(Interface)**를 통해 소통하는 관계.
*   **특징:**
    *   생성자 주입(Constructor Injection) 등을 통해 외부에서 객체를 받는다.
    *   구체 클래스가 무엇이든 인터페이스 규격만 맞으면 작동한다.
    *   확장성이 뛰어나며 유지보수 비용이 낮다.
*   **비유:** **배터리(Battery)**. 규격(인터페이스)만 맞으면 제조사(구체 클래스)에 상관없이 갈아끼울 수 있다.

---

## 3. 코드 구조 분석 (Code Analysis)

현재 프로젝트(`BossRaidPortfolio`)의 FSM 구조를 의존성 관점에서 분석한다.

### 3.1. 강한 의존성 구간 (Tight Coupling Areas)

이 구간은 의도적인 강한 결합을 통해 구현 편의성을 확보한 영역이다.

| 관계 (Dependency) | 분석 (Analysis) |
| :--- | :--- |
| `PlayerController` → `StateMachine` | **직접 생성 (`new`)**. 컨트롤러가 상태 머신의 생명주기를 완전히 관리하므로 강한 결합이 허용된다. |
| `MoveState` → `PlayerController` | **구체 클래스 참조**. 상태(State) 클래스들이 플레이어의 고유 기능(필드)에 빈번하게 접근해야 하므로, 문맥(Context) 공유를 위해 강한 의존성을 가진다. |

### 3.2. 약한 의존성 구간 (Loose Coupling Areas)

이 구간은 확장성과 유연성을 위해 인터페이스를 '다리(Bridge)'로 활용한 영역이다.

| 관계 (Dependency) | 분석 (Analysis) |
| :--- | :--- |
| **`DashState` → `IDashContext`** | **[핵심 패턴]** `DashState`는 `PlayerController`를 모른다. 오직 `IDashContext` 인터페이스만 바라본다. 향후 몬스터가 이 인터페이스를 구현하면 코드 수정 없이 대시 기능을 재사용할 수 있다. |
| `PlayerController` → `IInputProvider` | 입력 소스(키보드/네트워크/AI)를 몰라도 된다. `GetInput()` 메서드만 호출하면 되므로 입력 장치 교체가 자유롭다. |
| `PlayerController` → `IAttackable` | 콤보 데이터의 존재를 보장하는 계약(Contract) 관계. 외부 시스템은 플레이어/몬스터 구분 없이 이 인터페이스를 통해 공격 정보를 조회할 수 있다. |

---

## 4. 결론 (Conclusion)

의존성 자체는 나쁜 것이 아니다. 중요한 것은 **"어디서 끊고 어디서 묶을 것인가"**를 결정하는 것이다. 본 프로젝트는 핵심 로직(`DashState`)과 입력 시스템(`IInputProvider`)에 약한 의존성을 적용하여, 향후 멀티플레이어 및 AI 확장 시 발생할 수 있는 '코드 뜯어고치기' 비용을 최소화했다.
