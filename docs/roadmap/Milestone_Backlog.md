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

### 6주차: 내부 빌드 main branch 배포 & 멀티플레이 구현
#### 🚧 버그
- [x] 🟡**(보스, 피격) attack2 피격 크기, 피격 활성화시간**: 피격의 크기와 활성화시간을 수정하여 점프 때 근체 플레이어가 피해입게 만들기

#### 🚧 폴리싱
- [x] 🟢**(보스) attack2 로직 비주얼에서 분리**: boss model에 있는 damage cast를 로직에 옮기기
- [x] 🟢**지형 애셋 추가**: HQ Apocalyptic Environment 이 애셋을 이용해서 대체
- [x] 🟢**하늘 애셋 추가**: Fantasy Skybox FREE 이 애셋을 이용해서 대체


#### 내부 빌드 main branch 배포
- [x] 🟡**(개발, 버전관리) 메인 배포**: develop branch 내용 main에 커밋 및 푸시
- [x] 🟡**(개발, 멀티플레이) 계획 쓰기**: 어떤 멀티플레이를 구현할 건지 계획 작성.

#### 멀티플레이어
[x] `TitleScene` 멀티플레이 UI 구조 설계 확정

---

### 7주차: 멀티플레이 구현

- [x] 맵 & UI 본게임에 합치기
- [x] 파트너 UI 멀티일 때만 뜨게 하기
- [ ] 콤보 평소에는 끄고 있다가 콤보일때만 켜기
- [ ] 결과 후 승리/패배 나오게 하기
- [ ] 맵 장애물들을 용이 무시하게 하기 (길찾기 또는 장애물을 통과하게 하기)


#### 🚧 버그
- [x] 🟢**(보스, 공격범위)**: Attack1 editable range source를 `HeadDamageCaster.radius`로 단일화하고, `BossController.basicAttackRange`는 인스펙터에서 숨김 + legacy fallback으로만 유지.

#### 🚧 폴리싱
- [x] 🟡**(보스, attack1) 준비동작 수치조절**: 인스펙터에서 준비 시간 & 어느 동작까지가 준비동작인지 인스펙터에서 조절 가능하게 만들기

#### 멀티플레이어
- [x] NGO/UTP/Relay/Lobby 패키지 및 서비스 초기화
- [x] Host/Client lobby create/join 흐름 구현
- [ ] 2인 gameplay scene spawn 동기화
    - [x] Host는 Host player를 조작하고, Client는 Client player를 조작
    - [x] Host와 Client는 둘 다 서로의 캐릭터를 볼 수 있어야 함
    - [x] Host는 자기 카메라만 보고, Client도 자기 카메라만 봐야 함
    - [x] Host authority input path 정리: Client input -> Host receive/simulate -> result replicate, 그리고 client jitter 없이 이동해야 함
---

### 8주차: 멀티플레이 구현

- [ ] 콤보 평소에는 끄고 있다가 콤보일때만 켜기
- [ ] 결과 후 승리/패배 나오게 하기
- [ ] 맵 장애물들을 용이 무시하게 하기 (길찾기 또는 장애물을 통과하게 하기)


#### 멀티플레이어
- [x] NGO/UTP/Relay/Lobby 패키지 및 서비스 초기화
- [x] Host/Client lobby create/join 흐름 구현
- [x] 2인 gameplay scene spawn 동기화
    - [x] refactoring: PlayerController multiplayer부분 떼기.
    - [x] Host screen에서 Client player 애니메이션 움직이게 하기
- [ ] boss 구현
    - [ ] player multi authority 구현
    - [ ] boss authority host에 연동. 
    - [ ] boss 공격 및 피격 구현
    - [ ] boss aggro 구현

---


### 9주차: 멀티플레이 구현

- [x] 멀티플레이 초상화 host, client sync 맞추기

#### 멀티플레이어
##### 보스 (Boss)
1. [x] boss authority를 Host 기준으로 정리
    - victory sign if the boss defeated.
2. [x] boss 움직임 동기화
3. [x] boss 공격 동기화
    - projectiles in boss attack 3, 4. 
4. [x] boss 피격 동기화
    - client screen boss HP update
5. [x] boss aggro 구현

- [x] 밸런스 조절
    - [x] 밸런스 조절 툴 만들기
    - [x] 보스 앞발톱 뒤로는 플레이어가 가지않게 하기

- [ ] 멀티 기능과 그래픽 애셋 합치기
    - [ ] 밸런스값 넣기
    - [ ] 관련 브랜치들 main에 머지하기

#### 🚧 버그
- [x] 🟡**(플레이어, 애니메이션) locomotion 수정**: 반대방향으로 돌리면 속도가 줄어들 때 idle상태가 되어서 한프레임 때 서 있게 된다. 부드럽게 돌려서 계속 달리는 모션을 보여주자.
- [x] 🟢**(보스, 애니메이션) AoE 애니메이션 수정**: 클라이언트 화면에서 AoE Attack 중 보스 애니메이션이 나오게 수정.

#### 🚧 폴리싱
- [x] 🟢**(보스, basic attack) 공격범위 수정**: 구가 아니라 반구가 되게 변경하기
- [x] 🟢**(보스, basic attack & lunge attack) 공격 경로 추가**: 장판공격처럼 공격 범위를 추가하기


---
### 10주차: 아트 멀티 합치기

#### 버전 관리
- [x] feature/integration-art-multiplayer git diff 정리
- [ ] branch마다 커밋 안되는 애셋 나누고 구글드라이브에 올리기
    

#### 아트 멀티 합치기
##### art
- [ ] dust particle missing
- [x] Terrain 낮춰서 플레이어 대쉬 때 바닥 보이게 하기
- [x] press any key text panel 중간 아래에 보이게 하기
- [x] 캐릭터 이미지 아래에 나오게 하기
- [x] 아무 키를 누르면 `TitleRuntimeRoot`가 아니라 `TitleRuntimeRoot (1)`이 나타나도록 변경.
- [x] 대시 UI 기능 구현
- [x] 콤보 텍스트 감추기
- [ ] 라이트 플레이어 프리팹 교체하고 안나온다. 라이트 적용하기

##### programming
- [x] `Solo Play`를 누른 뒤 `Loading Scene`으로 넘어갈 때, `Assets/Scenes/mutiplayer/GamePlayScene.unity`로 전환되도록 변경.
- [x] 승리, 패배 이미지 보이게 하기
- [ ] Death Test 해제하기
###### 플레이어
- [ ] 플레이어 프리팹 비주얼 이식하기 (솔로플레이용, 멀티플레이용)
    - [x] 솔로
    - [ ] 멀티
- [x] 칼 사이즈 늘리기
- [x] 플레이어 신장에 콜라이더 맞추기
- [x] 플레이어 바닥 offset 줄이기
- [x] 피격 하얀색이 프리팹과 다름

###### 보스
- [ ] 보스 프리팹 비주얼 이식하기
- [ ] 보스 체력, 공격패턴 조정 (공격예상범위 넣기)
- [ ] 보스 발사체 비주얼 보이게 하기


#### 소리
- [ ] UI
- [ ] 배경음
- [ ] 플레이어
- [ ] 용

---

### 11주차: 아트 멀티 합치기

#### 버전 관리
- [x] feature/integration-art-multiplayer git diff 정리
- [ ] branch마다 커밋 안되는 애셋 나누고 구글드라이브에 올리기
    

#### 아트 멀티 합치기
##### art
- [x] dust particle missing
- [x] 라이트 플레이어 프리팹 교체하고 안나온다. 라이트 적용하기
- [x] 칼 이펙트 달리는 도중 나오는 걸 수정하기

##### programming
- [x] Death Test 해제하기
###### 플레이어
- [ ] 플레이어 프리팹 비주얼 이식하기 (솔로플레이용, 멀티플레이용)
    - [x] 솔로
    - [ ] 멀티



###### 보스
- [ ] 보스 프리팹 비주얼 이식하기
- [ ] 보스 체력, 공격패턴 조정 (공격예상범위 넣기)
- [ ] 보스 발사체 비주얼 보이게 하기


#### 소리
- [ ] UI
- [ ] 배경음
- [ ] 플레이어
- [ ] 용

---


## 멀티플레이어
##### 공통 (General)
1. [x] `TitleScene` 멀티플레이 UI 구조 설계 확정
2. [x] NGO/UTP/Relay/Lobby 패키지 및 서비스 초기화
2.1 [x] package 준비 세션
2.2 [x] UGS bootstrap 세션
2.3 [x] Host create 세션
2.4 [x] Client join 세션
2.5 [x] Lobby active 세션
2.6 [x] gameplay start 세션
2.7 [x] strict cleanup 세션
3. [x] Host/Client lobby create/join 흐름 구현
4. [x] (싱글, 멀티) 다시 플레이 만들기

##### 플레이어 (Player)
1. [x] 2인 gameplay scene spawn 동기화
2. [x] 이동 동기화: `client prediction + Host authoritative reconcile`
3. [x] 플레이어 권한 규칙 정리: `client sends intent, Host decides result`
4. [x] 대쉬 동기화: `local anticipation + Host correction`
5. [x] 공격 동기화: `Attack1 / Attack2 / Attack3`의 시작, 판정, 데미지 결과를 Host authority로 처리
6. [x] 상대 플레이어 표시 복제: remote partner의 `move / dash / attack / hit / stun / death`를 display-only 경로로 재생
7. [x] 플레이어 HP/HUD 동기화: Host authoritative HP 기준으로 self / partner HUD 갱신

##### 보스 (Boss)
1. [x] boss authority를 Host 기준으로 정리
2. [x] boss 움직임 동기화
3. [x] boss 공격 동기화
4. [x] boss 피격 동기화
5. [x] boss aggro 구현

##### 구현 안된 것

---
## 아트 멀티 합치기
### 아트
- [ ] Assets/Scenes/Legacy/GamePlayScene_Art3.unity 바닥 매터리얼 복구하기
- [ ] 칼 vfx 수정하기
- [ ] 대시 UI 작동하게 하기
- [ ] Assets/Scenes/mutiplayer/LoadingScene.unity 로딩씬 작동하게 하기
- [ ] Assets/Scenes/mutiplayer/GamePlayScene.unity player1, 2를 feature/multiplayer multiplayerAvatar prefab 쓰고 거기에 player1, 2의 애셋을 쓰게 하기. 
### 멀티
- [ ] feature/multiplayer titleScene에 있는 Solo, multiplayer UI, 패널을 feature/integration-art-multiplayer Assets/Scenes/mutiplayer/TitleScene.unity 여기에 넣기
- [ ] Assets/Scenes/GamePlayScene_Art3.unity 여기에 feature/multiplayer에 있는 prefabs/boss, multiplayerAvater, singleplayer를 넣기
- [ ] feature/integration-art-multiplayer Assets/Scenes/mutiplayer 폴더 안에서 titleScene -> LoadingScene -> GamePlayScene 흐름 연결하기
- [ ] Assets/Scenes/mutiplayer/GamePlayScene.unity player UI 배치 host, client 따라 바뀌게 정하기


---

## 🚧 버그

- [x] 🔴**(플레이어) 대쉬 방향 수정**: 공격 방향이 아닌 키보드 입력 방향으로 대쉬.
- [x] 🔴**(보스) 스크림 애니메이션 수정**: 범위 안에서 플레이어가 걸어오면 스크림 애니메이션 발동.
- [x] 🔴**(보스, attack) 패턴별 동적 공격 사거리 적용**: 패턴 1은 근거리 사거리, 그 외 패턴은 더 긴 사거리 사용.
- [x] 🔴**(보스, phase1) 플레이어 거리에 따른 공격 선택**: Basic/Lunge 범위를 동시에 만족하면 Basic 우선, Basic 범위를 벗어나고 Lunge 범위를 만족하면 Lunge 선택.
- [x] 🔴**(보스, attack1) Basic 사거리-판정 반경 동기화**: `basicAttackRange`와 `HeadDamageCaster` 반경(`radius`)을 자동 동기화.
- [x] 🔴**(보스) attack2 도약 회귀 수정**: 도약을 하면 도약 전 위치로 돌아오지 않고 도약한 곳으로 고정.
- [x] 🟡**(보스) attack2 플레이어 위에 서는 것 수정**: 도약을 하기 전 걸어 올 때 플레이어의 머리 위로 올라온다.
- [x] 🟡**(보스, AoE) 반경 기반 데미지 판정 수정**: Projectile 충돌이 아니라 AoE Circle의 반경 기준으로 플레이어 데미지를 적용한다. Circle이 fully red(텔레그래프 종료 시점) 상태가 되면 반경 내 플레이어에게 데미지가 들어가야 한다.
- [x] 🟡**(플레이어, 카메라) 리팩토링**: 좌우 이동할 때 카메라 턱턱 걸리지 않고 부드럽게 움직이게 하기.
- [x] 🟡**(플레이어) Attack3 넉백 거리 수정**: Projectile로 스턴생기면 stun distance보다 멀리 나간다. 넉백길이 일정하게 수정
- [x] 🟡**(플레이어, 애니메이션) client 공격 우선순위 고치기**: 이동 또는 대쉬 때 공격을 하면 공격이 우선적으로 실행된다. 이동은 상관없지만 대쉬는 캔슬이 되면 안된다. 대쉬 캔슬이 되지 않게 만들기.
- [x] 🟡**(플레이어, 애니메이션) dash 수정**: idle상태에서 dash를 하면 부드럽게 대쉬를 하지 않는다. 대신 방향키를 누른 상태에서 대쉬를 한다면 부드럽게 됐다. idle 상태에서도 대쉬 부드럽게 하기
- [x] 🟡**(플레이어, 애니메이션) locomotion 수정**: 반대방향으로 돌리면 속도가 줄어들 때 idle상태가 되어서 한프레임 때 서 있게 된다. 부드럽게 돌려서 계속 달리는 모션을 보여주자.
- [ ] ~~🟡**(플레이어, 애니메이션) dash 상대화면에서 부드럽게 출력**: 나의 dash가 상대 화면에서 잔상이 남기면서 진행된다. 이걸 방지하고 부드럽게 연출되게 하자.~~
- [ ] 🟡**(보스, 멀티플레이) 회전 지지직**: 회전할 때 부드럽게 회전하도록 하기
- [ ] 🟡**(플레이어, 콜리젼) 멀티 때 서로 닿지 않게 만들기**: 멀티일 때 서로 겹쳐서 지직거린다. 겹치지 않게 마진을 주자. 
- [ ] 🟡**(보스, 애니메이션) 2페이즈 생략**: 페이즈 변환할 때 때려도 페이즈변환 무시 안되게 하기.
- [x] 🟢**(리포지토리) 보스 모델 공유 누락 수정**: `FourEvilDragonsPBR/DragonUsurper`의 `Red.prefab(.meta)`와 `UNI VFX`의 `Crushing Pull Gold.prefab(.meta)`만 `.gitignore` 예외로 허용하도록 규칙을 조정
- [x] 🟢**(보스, 공격범위)**: Attack1 editable range source를 `HeadDamageCaster.radius`로 단일화하고, `BossController.basicAttackRange`는 인스펙터에서 숨김 + legacy fallback으로만 유지.
- [ ] 🟢**(플레이어, UI) 데미지 끄기**: 보스 죽고 보스 때려도 때리는 UI 나오지 않게 하기
- [ ] 🟢**(플레이어, UI)**: 3번째 공격 UI가 1초 느리게 나온다. 바로 바로 나오는 UI로 수정.
- [x] 🟢**(보스, Attack1) 첫공격만 준비동작 생략**: 첫공격도 준비동작 있게 하기
~~- [ ] 🟢**(보스) attack2 모델과 로직의 싱크**: 도약을 하면 부모 gameObject가 애니메이션을 못따라간다. (보류)~~
- [ ] ~~🟢**(플레이어)**: PlayerController 인스펙터에서 HUD 켜고 끄는 체크박스 기능 만들기, HUD위치 Top center로 변환.~~


---

## 🚧 폴리싱

- [x] 🔴**(플레이어) 넉백 추가하기**: 플레이어가 보스에게 피격당했을 때 넉백 추가하기
- [x] 🔴**(보스) 피격 받는 중 공격 시전 유지**: 피격 애니메이션에 의해 공격 시전이 끊기지 않도록 하고, 데미지는 정상 반영.
- [x] 🟡**(플레이어, 스턴) 리팩토링**: 넉백 길이 짧게 하기.
- [x] 🟡**(보스, attack1) 준비동작 수치조절**: 인스펙터에서 준비 시간 & 어느 동작까지가 준비동작인지 인스펙터에서 조절 가능하게 만들기
- [x] 🟡**(플레이어, UI) 콤보**: 콤보 기능 넣기
- [x] 🟢**지형 애셋 추가**: HQ Apocalyptic Environment 이 애셋을 이용해서 대체
- [x] 🟢**하늘 애셋 추가**: Fantasy Skybox FREE 이 애셋을 이용해서 대체
- [x] 🟢**글씨 다르게 하기**: 글씨 다르게 하기
- [x] 🟢**(게임매니저) 리팩토링**: Victory, GameOver TextArea로 만들기
- [x] 🟢**(플레이어, UI) 리팩토링**: HUD 끄기
- [x] 🟢**(보스) Attack2 공격가능한 애니메이션**: 애니메이션에서 점프 후 때만 공격이 활성화 되게 하기
- [x] 🟢**(보스, 컴포넌트) 애니메이터 컴포넌트**: 프리팹은 프리팹만 있게 하기. 비주얼 
로직은 비주얼 gameobject에 넣기
- [x] 🟢**(플레이어, UI) 콤보 구현**: 때릴 때 콤보 넣게 하기
- [x] 🟢**(보스, basic attack) 공격범위 수정**: 구가 아니라 반구가 되게 변경하기
- [x] 🟢**(보스, basic attack & lunge attack) 공격 경로 추가**: 장판공격처럼 공격 범위를 추가하기
- [ ] 🟡**(보스, 패턴 추가) 스프레이 불 추가**: attack5로 스프레이 불 넣기
- [ ] ~~🟡**(보스) 회전 수정**: 플레이어를 항상 쳐다보는 것이 아닌 공격을 하기 전, 공격을 한 직후는 boss rotation 고정.~~
- [ ] ~~🟡**(보스, attack5) attack4 형태를 한꺼번에 나오게 하기**: attack4가 하나씩 떨어졌다면 attack5는 한꺼번에 맵 전역에 떨어지게 하기.~~
- [ ] ~~🟡**(보스) 패턴 3 투사체 조준 보정**: 플레이어가 드래곤 뒤쪽에 있어도 플레이어 방향으로 투사체 발사.~~
- [ ] ~~🟡**다중 레이캐스트 탐지**: 눈 위치(몸통 1/2~머리 중간)에서 여러 방향 감지.~~
- [ ] ~~🟢**(보스) Run/Walk 애니매이션 추가**: Run 애니메이션 추가 및 그에 맞는 보스 움직임 속도 부드럽게 증가시키기~~
- [ ] ~~🟢**추적 알고리즘 검토**: NavMesh / A* 경로탐색 적용 여부 결정.~~
- [ ] ~~🟢**(보스, VFX) 피격 플래시 이펙트**: `BaseVisual.FlashRoutine`을 Emission 기반으로 변경 (`material.SetColor("_EmissionColor")`) 또는 머티리얼 교체 방식 적용.~~
- [ ] ~~🟢**(UI) 움직임 추가**: Press Any Key 깜빡깜빡 효과 추가. fade in & out.~~
- [ ] ~~🟢**(플레이어) 리팩토링**: DashState Enter(), input데이터 직접참조를 Update(Input) 여기서 받게 수정.~~
- [ ] ~~🟢**(플레이어, 애니메이션) 맞는 모션 2개로 변경**: 맞는 모션을 2개로 해서 모션 다양하게 하기.~~
- [ ] ~~🟢**(보스, UI) 리팩토링**: 플레이어가 detect range 안에 있을 때 보스의 체력 UI를 나타나게 하기~~

---

### 📝 공통 문서화 작업
- **책임(Responsibility) 문서화**: 로직별 책임 소재를 코드와 글로 명확히 설명 (면접 대비).
