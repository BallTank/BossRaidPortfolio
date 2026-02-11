# ⚔️ Boss Raid Action (Project Portfolio)

![Gameplay Preview](https://via.placeholder.com/800x400?text=Please+Insert+Gameplay+GIF+Here)
> **"Solid Architecture, Zero-GC, Network-Ready Input"**
>
> Unity C# 기반의 3D 보스 레이드 액션 프로젝트입니다.
> **확장성 있는 FSM 설계**와 **가비지 컬렉션(GC) 최소화**를 목표로 개발되었습니다.

---

## 🏗️ Technical Highlights (핵심 기술)

이 프로젝트는 단순 구현을 넘어, **유지보수성**과 **최적화**를 고려하여 설계되었습니다.

### 1. Generic FSM Architecture (확장성)
*   **문제**: 플레이어와 보스의 상태 로직이 중복되고, `enum` 기반 분기가 비대해지는 문제.
*   **해결**: `StateMachine<TState>` 제네릭 클래스로 FSM을 통합하고, 각 상태를 클래스로 분리하여 **SRP(단일 책임 원칙)** 를 준수했습니다.
*   **결과**: 새로운 패턴 추가 시 기존 코드를 수정할 필요 없이 클래스만 추가하면 되는 **OCP(개방-폐쇄 원칙)** 구조 완성.
    *   📄 [관련 설계 문서: System Blueprint](docs/System_Blueprint.md)

### 2. Zero-Alloc Physics (최적화)
*   **문제**: 매 프레임 발생하는 물리 연산(`OverlapSphere`)으로 인한 잦은 GC 발생.
*   **해결**: `Physics.OverlapSphereNonAlloc`을 도입하고, 충돌 검사 결과를 저장하는 배열을 **Pre-allocate(미리 할당)** 하여 런타임 메모리 할당을 **0(Zero)** 으로 만들었습니다.
    ```csharp
    // 최적화 예시 코드 (단순화)
    private readonly Collider[] _hitResults = new Collider[10]; 
    public void CheckHit() {
        int count = Physics.OverlapSphereNonAlloc(..., _hitResults); // No GC Allocation
    }
    ```

### 3. Network-Ready Input System (설계)
*   **특징**: `Input.GetKey`를 로직에서 직접 호출하지 않고, `IInputProvider` 인터페이스를 통해 입력과 로직을 분리했습니다.
*   **Bit-Packing**: 네트워크 동기화를 염두에 두고, 버튼 입력을 `struct` 내의 **Bitmask**로 처리하여 패킷 용량을 최적화했습니다.

---

## 🛠️ Stack & Tools

*   **Engine**: Unity 6 (6000.0.x)
*   **Language**: C# (Style Guide 준수)
*   **Version Control**: Git (Git Flow 전략 사용 - [전략 문서](docs/Git_Branching_Strategy.md))
*   **Documentation**: Markdown, Mermaid JS (Class Diagrams)

---

## 📂 Project Structure

물리적 폴더 구조와 논리적 네임스페이스를 `Core` 하위로 일치시켜 의존성을 관리했습니다.

```text
Assets/Scripts/Core
├── Common/    # (Generic) StateMachine, Health, IDamageable
├── Player/    # (Unique) PlayerController, States, InputProvider
└── Boss/      # (Unique) BossController, AI Strategies, Visual
```

---

## 📚 Technical Documentation & Blog

개발 과정에서 마주친 기술적 난제와 의사결정 과정을 상세히 기록했습니다.

### Core Architecture
| Document | Description |
| --- | --- |
| 🏗️ **[System Blueprint](docs/System_Blueprint.md)** | 전체 클래스 다이어그램 및 시스템 아키텍처 설계도 |
| 📖 **[Coding Standard](docs/Coding_Standard.md)** | 프로젝트 코딩 컨벤션 및 최적화 가이드라인 |
| 📅 **[Progress Log](docs/Progress_Log.md)** | 일일 개발 일지 및 주요 마일스톤 달성 현황 |

## 📧 Contact

*   **이름**: 이종휘
*   **Email**: osmf12@naver.com
