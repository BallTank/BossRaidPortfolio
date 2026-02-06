# 🎮 Game Design Document: 정육면체 2P 보스 레이드

**3D P2P 멀티플레이어 보스 레이드 포트폴리오**

---

## 1. 프로젝트 개요

| 항목 | 설명 |
|------|------|
| **플랫폼** | PC (Windows) |
| **엔진** | Unity 2D/3D (Universal Render Pipeline) |
| **장르** | 3D 액션 RPG / 보스 레이드 |
| **인원** | 2인 협동 (P2P 멀티플레이어) |
| **핵심 컨셉** | 포스트 아포칼립스 세계관에서 기하학적 형상의 거대 보스와 맞서 싸우는 스타일리시 액션. |

---

## 2. 기술 아키텍처

> **핵심 가치**: "네트워크 최적화와 안정적인 데이터 동기화 구현"

### 2.1 P2P 네트워킹 모델 (Listen-Server)

| 역할 | 설명 |
|------|------|
| **호스트(Host)** | 플레이어 1의 클라이언트가 서버 역할을 겸임. 보스 AI(FSM), 보스 HP, 승리/패배 판정의 최종 권한(Authority)을 가짐. |
| **클라이언트(Client)** | 플레이어 2. 자신의 캐릭터 이동 및 공격을 담당하며, 호스트로부터 보스의 상태 데이터를 수신하여 화면에 렌더링. |

**동기화 전략:**
- `NetworkVariable`: 보스 HP 및 페이즈 상태 동기화.
- `RPC (Remote Procedure Call)`: 보스의 공격 패턴 트리거 및 데미지 판정 요청.

### 2.2 최적화 및 메모리 관리 (Zero-Allocation Architecture)

> **핵심 목표**: "전투 중 발생하는 Managed Heap 할당을 최소화하여 GC 스파이크 없는 60FPS 환경 구현"

#### ① 고성능 오브젝트 풀링 (Projectile & VFX Management)
- **Pre-warm 시스템**: 전투 진입 전(Loading Phase) 필요한 최대 수량의 객체를 미리 생성.
- **Runtime Zero-Allocation**: `Instantiate`와 `Destroy` 대신 `SetActive(true/false)` 사용.
- **구현 위치**: `ProjectileManager`, `VFXManager`

#### ② NonAlloc API를 활용한 물리 판정 (Physics Optimization)
- `Physics.OverlapSphereNonAlloc`, `RaycastNonAlloc` 사용.
- **Array Reuse**: 클래스 멤버 변수로 미리 선언한 `Collider[]` 배열을 재사용.
- **구현 위치**: `BossAttackController`, `HitDetectionSystem`

#### ③ 대역폭 최적화 (Deterministic Pattern Sync)
- **Pattern-based RPC**: 100개 투사체의 Transform 대신 '패턴 ID'와 '랜덤 시드(Seed)' 값만 전송.
- **Local Calculation**: 클라이언트는 수신한 시드값을 바탕으로 로컬에서 동일한 궤적을 계산.

---

## 3. 게임 플레이 메커니즘

### 3.1 플레이어 캐릭터 (Katana Specialist)

| 액션 | 설명 |
|------|------|
| **기본 공격** | 콤보 시스템 기반의 경공격(Light) 및 강공격(Heavy). |
| **특수 이동 (Dash)** | 일정 거리 고속 이동. 최대 2회 연속 사용 가능. 쿨타임 3초. |
| **입력 시스템** | Unity New Input System 적용 (키보드/마우스 및 게임패드 지원). |

### 3.2 보스: 기하학적 파수꾼 (The Geometric Sentinel)

| 패턴 | 설명 |
|------|------|
| **탄막 세례(Barrage)** | 보스 몸체에서 수십 개의 작은 기하학체 투사체가 플레이어를 추적. |
| **전역 범위 공격(Field Wipe)** | 맵 전체에 레드 존 표시. 안전 구역으로 이동하지 않으면 즉사급 데미지. |
| **급습(Emerging)** | 지면 아래로 사라진 후, 플레이어 위치를 예측하여 공중에서 수직 낙하 공격. |

**외형**: 거대한 큐브 또는 구체 형태. 페이즈에 따라 형상이 분리되거나 합쳐지는 절차적 애니메이션.

---

## 4. 데이터 및 도구 (Data & Tooling)

### 4.1 디자이너 유틸리티 툴 (Editor Window)
- **체크박스**: 무적 모드, AI 활성화 여부.
- **슬라이더**: 보스 이동 속도, 투사체 발사 간격, 대시 쿨타임(기본 3.0s).
- **데이터 형식**: `ScriptableObject`를 활용한 데이터 관리.

---

## 5. 아트 및 에셋 (Assets & Theme)

| 항목 | 에셋명 |
|------|--------|
| **배경** | HQ Apocalyptic Environment (포스트 아포칼립스 대도시) |
| **플레이어** | CombatGirls_KatanaCharacterPack (애니메이션: Adobe Mixamo) |
| **몬스터** | OldMan Zombie (일반 잡몹용) |
| **보스** | Procedural Geometry (기본 큐브/구체 + 커스텀 셰이더) |

---

## 6. 승리 및 패배 조건 (Game Loop)

| 조건 | 설명 |
|------|------|
| **승리** | 보스의 HP를 0으로 만듦. |
| **패배** | 모든 플레이어의 HP가 0이 됨 (타임 어택 없음). |
| **종료** | 결과 연출 후 메인 로비로 복귀. |
