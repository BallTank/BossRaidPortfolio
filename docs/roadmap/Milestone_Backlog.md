# 🗂️ Milestone Backlog

이 문서는 마일스톤 진행 현황, 버그, 폴리싱 항목을 관리하는 백로그 전용 문서입니다.

## 📈 마일스톤: 싱글플레이 로직 완성 (Capsule vs Cube)

> **목표**: 클라이언트 구축

### 1주차: 플레이어 컨트롤러 & 전투 시스템 (The Capsule & Sword) ✅
- [x] **Input System**: 키보드/마우스 및 패드 입력을 인터페이스화해서 분리.
- [x] **FSM (State Machine)**: `PlayerState` 클래스 기반의 상태 머신 (Idle, Move, Attack, Dash).
- [x] **이동 로직**: `CharacterController`를 이용한 물리 기반 이동.
- [x] **핵심**: 입력과 로직 분리 완료. (네트워크 입력 교체 대비)
- [x] **에셋 교체**: `CombatGirls_KatanaCharacterPack` 연동 및 `Visual` 계층 분리 (`Animator_Setup_Guide.md` 준수).
- [x] **Animator 설정**: `Locomotion`, `Attack1~3`, `Jump`, `Quickshift_F` 상태 연결.
- [x] **Hitbox 시스템**: 칼이 휘둘러질 때 특정 프레임에서만 판정이 생기도록 설계.
- [x] **피격 시스템**: 플레이어/보스가 데미지를 받았을 때의 반응 (HP 감소, 피격 애니메이션, 무적 시간).
- [x] **Damage 클래스**: `IDamageable` 인터페이스 (보스/잡몹 공통 상속).
- [x] **판정 최적화**: `Physics.OverlapSphereNonAlloc` 사용.
- [x] **핵심**: 코드가 판정 시점을 정확히 계산하는 것을 보여줘야 함.

---

### 2주차: 보스 AI 패턴 (The Cube)

#### 보스 FSM 아키텍처 ✅
- [x] **StateMachine 제네릭화**: `StateMachine<TState>`로 Player/Boss 공용 상태 머신 통합.
- [x] **BossBaseState**: `BaseState<BossController>` 상속, `Update()` 입력 없이 내부 로직만 처리.
- [x] **IState 인터페이스**: `Enter()`/`Exit()` 공통 계약 정의로 StateMachine의 구체 타입 의존 제거.
- [x] **BossVisual 분리**: 애니메이션/UI 제어를 `BossVisual` 클래스로 이관하여 SRP 준수.

#### 피격 로직 (Player & Boss 공통) ✅
- [x] **IDamageable 인터페이스**: 공격자가 타겟 타입을 몰라도 데미지 전달 가능.
- [x] **Health 컴포넌트**: HP 관리, `OnDamage`/`OnDie` 이벤트 발행.
- [x] **DamageCaster**: `OverlapSphereNonAlloc`으로 GC-Free 타격 판정.
- [x] **Event-Driven Death**: `OnDie` 이벤트 구독으로 `DeadState` 전환.

#### 보스 행동 패턴 ✅
- [x] **패턴 1 (추적)**: 플레이어와의 거리 계산, 적정 거리 유지 및 공격 유도.
- [x] **근접 공격 (BasicAttackPattern)**: `IBossAttackPattern` Strategy Pattern 적용. 쿨다운 시스템.
- [x] **패턴 2 (돌격)**: 예고 표시 후 돌진.
- [ ] **패턴 3 (투사체)**: 큐브에서 작은 큐브(미사일) 발사.

---

### 3주차: 보스 행동 패턴 & 시스템 최적화 & 게임 루프 (The Logic)

#### 보스 행동 패턴 ✅
- [x] **패턴 3 (투사체)**: 큐브에서 작은 큐브(미사일) 발사 및 애셋 입히기.
- [x] **패턴 4 (장판)**: 하늘에서 땅을 향해 불 발사.

#### 시스템 최적화 & 게임 루프 (The Logic)
- [x] **오브젝트 풀링**: 미사일/이펙트를 Zero-Allocation으로 관리.

---

### 4주차: 보스 행동 패턴 & 시스템 최적화 & 버그
#### UI ✅
- [x] **UI 시스템**: 보스/플레이어 HP 바와 고정형 데미지 피드백을 이벤트 기반으로 코드 제어 + HUD 배치 완료.
- [x] **HUD 골격 배치**: `CombatHUDController` + 이름 라벨(`Player`, `Dragon`) + 고정형 데미지 텍스트 앵커 구성.
- [x] **HP 바 자동 갱신 경로**: `Health.OnDamageTaken`/`OnDeath` 기반 UI 갱신 연결.
- [x] **공격 윈도우 결과 이벤트**: `DamageCaster.OnAttackWindowResolved` 추가 및 `PlayerController` HUD 연결.

#### 게임 루프 (The Logic) ✅
- [x] **실플레이 검증**: 피격 1회/콤보/사망 직전 케이스에서 중복 이벤트 및 UI 갱신 타이밍 점검.
- [x] **게임 매니저(시작 -> 전투 페이즈 전환)**: 구현 완료 (`TitleSceneController` + `SceneLoader` + `LoadingSceneController` 경유 전투 진입 경로 구성), 실플레이 테스트 미실시.
- [x] **게임 매니저(승리/패배)**: 구현 완료 (`GameManager` 결과 판정/UI/재시작 입력 처리), 동시 쓰러질 때 `Victory` 출력 테스트 완료 (`GamePlayScene_TestResult` + `SimultaneousDeathTest`).

#### 게임 루프 (The Logic) ✅
- [x] 🔴**(보스) 스크림 애니메이션 수정**: 범위 안에서 플레이어가 걸어오면 스크림 애니메이션 발동.
- [x] 🔴**(보스, attack) 패턴별 동적 공격 사거리 적용**: 패턴 1은 근거리 사거리, 그 외 패턴은 더 긴 사거리 사용.

---

### 5주차: 버그 및 폴리싱

#### 🚧 버그
- [x] 🔴**(보스, phase1) 플레이어 거리에 따른 공격 선택**: Basic/Lunge 범위를 동시에 만족하면 Basic 우선, Basic 범위를 벗어나고 Lunge 범위를 만족하면 Lunge 선택.
- [x] 🔴**(보스, attack1) Basic 사거리-판정 반경 동기화**: `basicAttackRange`와 `HeadDamageCaster` 반경(`radius`)을 자동 동기화.
- [x] 🔴**(보스) attack2 도약 회귀 수정**: 도약을 하면 도약 전 위치로 돌아오지 않고 도약한 곳으로 고정.
- [x] 🟡**(보스) attack2 플레이어 위에 서는 것 수정**: 도약을 하기 전 걸어 올 때 플레이어의 머리 위로 올라온다.
- [x] 🟢**(리포지토리) 보스 모델 공유 누락 수정**: `FourEvilDragonsPBR/DragonUsurper`의 `Red.prefab(.meta)`와 `UNI VFX`의 `Crushing Pull Gold.prefab(.meta)`만 `.gitignore` 예외로 허용하도록 규칙을 조정

#### 🚧 폴리싱
- [x] 🔴**(플레이어) 넉백 추가하기**: 플레이어가 보스에게 피격당했을 때 넉백 추가하기
- [x] 🔴**(보스) 피격 받는 중 공격 시전 유지**: 피격 애니메이션에 의해 공격 시전이 끊기지 않도록 하고, 데미지는 정상 반영.
- [x] 🟡**(플레이어, 스턴) 리팩토링**: 넉백 길이 짧게 하기.

---
#### 🚧 버그

- [x] 🔴**(플레이어) 대쉬 방향 수정**: 공격 방향이 아닌 키보드 입력 방향으로 대쉬.
- [x] 🔴**(보스) 스크림 애니메이션 수정**: 범위 안에서 플레이어가 걸어오면 스크림 애니메이션 발동.
- [x] 🔴**(보스, attack) 패턴별 동적 공격 사거리 적용**: 패턴 1은 근거리 사거리, 그 외 패턴은 더 긴 사거리 사용.
- [x] 🔴**(보스, phase1) 플레이어 거리에 따른 공격 선택**: Basic/Lunge 범위를 동시에 만족하면 Basic 우선, Basic 범위를 벗어나고 Lunge 범위를 만족하면 Lunge 선택.
- [x] 🔴**(보스, attack1) Basic 사거리-판정 반경 동기화**: `basicAttackRange`와 `HeadDamageCaster` 반경(`radius`)을 자동 동기화.
- [x] 🔴**(보스) attack2 도약 회귀 수정**: 도약을 하면 도약 전 위치로 돌아오지 않고 도약한 곳으로 고정.
- [x] 🟡**(보스) attack2 플레이어 위에 서는 것 수정**: 도약을 하기 전 걸어 올 때 플레이어의 머리 위로 올라온다.
- [x] 🟡**(보스, AoE) 반경 기반 데미지 판정 수정**: Projectile 충돌이 아니라 AoE Circle의 반경 기준으로 플레이어 데미지를 적용한다. Circle이 fully red(텔레그래프 종료 시점) 상태가 되면 반경 내 플레이어에게 데미지가 들어가야 한다.
- [ ] 🟡**(플레이어, 카메라) 리팩토링**: 좌우 이동할 때 카메라 턱턱 걸리지 않고 부드럽게 움직이게 하기.
- [ ] 🟡**(플레이어) Attack3 넉백 거리 수정**: Projectile로 스턴생기면 stun distance보다 멀리 나간다. 넉백길이 일정하게 수정
- [ ] 🟡**(보스, 애니메이션) 2페이즈 생략**: 페이즈 변환할 때 때려도 페이즈변환 무시 안되게 하기.
- [x] 🟢**(리포지토리) 보스 모델 공유 누락 수정**: `FourEvilDragonsPBR/DragonUsurper`의 `Red.prefab(.meta)`와 `UNI VFX`의 `Crushing Pull Gold.prefab(.meta)`만 `.gitignore` 예외로 허용하도록 규칙을 조정
- [ ] 🟢**(플레이어, UI)**: 3번째 공격 UI가 1초 느리게 나온다. 바로 바로 나오는 UI로 수정.
- [ ] 🟢**(플레이어)**: PlayerController 인스펙터에서 HUD 켜고 끄는 체크박스 기능 만들기, HUD위치 Top center로 변환.
- [ ] 🟢**(보스) attack2 모델과 로직의 싱크**: 도약을 하면 부모 gameObject가 애니메이션을 못따라간다. (보류)


---

#### 🚧 폴리싱

- [x] 🔴**(플레이어) 넉백 추가하기**: 플레이어가 보스에게 피격당했을 때 넉백 추가하기
- [x] 🔴**(보스) 피격 받는 중 공격 시전 유지**: 피격 애니메이션에 의해 공격 시전이 끊기지 않도록 하고, 데미지는 정상 반영.
- [x] 🟡**(플레이어, 스턴) 리팩토링**: 넉백 길이 짧게 하기.
- [ ] 🟡**(보스) 회전 수정**: 플레이어를 항상 쳐다보는 것이 아닌 공격을 하기 전, 공격을 한 직후는 boss rotation 고정.
- [ ] 🟡**(보스, attack5) attack4 형태를 한꺼번에 나오게 하기**: attack4가 하나씩 떨어졌다면 attack5는 한꺼번에 맵 
전역에 떨어지게 하기. 
- [ ] 🟡**(보스) 패턴 3 투사체 조준 보정**: 플레이어가 드래곤 뒤쪽에 있어도 플레이어 방향으로 투사체 발사.
- [ ] 🟡**다중 레이캐스트 탐지**: 눈 위치(몸통 1/2~머리 중간)에서 여러 방향 감지.
- [ ] 🟢**지형 애셋 추가**: HQ Apocalyptic Environment 이 애셋을 이용해서 대체
- [ ] 🟢**(보스, VFX) 피격 플래시 이펙트**: `BaseVisual.FlashRoutine`을 Emission 기반으로 변경 (`material.SetColor("_EmissionColor")`) 또는 머티리얼 교체 방식 적용.
- [ ] 🟢**글씨 다르게 하기**: 글씨 다르게 하기
- [ ] 🟢**(보스) Run/Walk 애니매이션 추가**: Run 애니메이션 추가 및 그에 맞는 보스 움직임 속도 부드럽게 증가시키기
- [ ] 🟢**추적 알고리즘 검토**: NavMesh / A* 경로탐색 적용 여부 결정.
- [ ] 🟢**(플레이어) 리팩토링**: DashState Enter(), input데이터 직접참조를 Update(Input) 여기서 받게 수정.
- [x] 🟢**(게임매니저) 리팩토링**: Victory, GameOver TextArea로 만들기
- [ ] 🟢**(UI) 움직임 추가**: Press Any Key 깜빡깜빡 효과 추가. fade in & out.
- [ ] 🟢**(보스, UI) 리팩토링**: 플레이어가 detect range 안에 있을 때 보스의 체력 UI를 나타나게 하기
- [ ] 🟢**(플레이어, UI) 리팩토링**: HUD 끄기
- [ ] 🟢**(보스) Attack2 공격가능한 애니메이션**: 애니메이션에서 점프 후 때만 공격이 활성화 되게 하기
- [ ] 🟢**(플레이어, 애니메이션) 맞는 모션 2개로 변경**: 맞는 모션을 2개로 해서 모션 다양하게 하기.
- [ ] 🟢**(보스, 컴포넌트) 애니메이터 컴포넌트**: 프리팹은 프리팹만 있게 하기. 비주얼 로직은 비주얼 gameobject에 넣기


---

### 📝 공통 문서화 작업
- **책임(Responsibility) 문서화**: 로직별 책임 소재를 코드와 글로 명확히 설명 (면접 대비).