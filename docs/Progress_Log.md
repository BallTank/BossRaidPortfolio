# 🚀 Progress Log: Boss Raid Portfolio

## 📅 [2월 1주차] 목표: 플레이어 컨트롤러 및 상태 머신

### **2026-02-02 (월): 기획 및 아키텍처 설계**

* **오늘 반영한 작업**
* GDD(Technical Architecture) 초안 작성 및 시스템 로드맵 수립.
* 입력 시스템 인터페이스(IInputProvider) 및 데이터 구조(PlayerInputPacket) 의사코드 설계.

* **기술적 고려**
* 입력/로직 분리를 전제로 설계해 추후 NetworkInputProvider 교체가 가능한 구조를 확보.
* 초기 단계부터 구조체 기반 데이터 전달을 채택해 성능 및 직렬화 확장성을 확보.

### **2026-02-03 (화): 입력 시스템 및 코어 모터 구현**

* **오늘 반영한 작업**
* Unity Input System 연동 및 PlayerControlInput 바인딩.
* PlayerInputPacket 비트 패킹 적용.
* 카메라 기준 이동(Camera-Relative Movement) 및 CameraRoot/Body 회전 분리.

* **기술적 고려**
* 버튼 상태를 비트 연산으로 묶어 패킷 크기를 최소화.
* 이동 벡터 계산에서 Y 성분을 제거해 조작 일관성을 확보.

### **2026-02-04 (수): FSM 리팩터링 및 Dash/Jump/Attack 상태 구현**

* **오늘 반영한 작업**
* StateMachine/BaseState 구현 및 MoveState 이관.
* DashState 쿨타임/엣지 트리거 적용.
* JumpState 공중 제어 및 착지 판정 구현.
* AttackState 3단 콤보, 선입력, 대시 캔슬 구현.

* **기술적 고려**
* 컨트롤러는 컨텍스트/실행, 상태는 판단/전환을 담당하도록 책임 분리.
* 입력 엣지 트리거로 연속 입력 부작용을 차단.

### **2026-02-05 (목): 코드베이스 분석 및 문서화 동기화**

* **오늘 반영한 작업**
* 의존성 분석 및 FSM 구조 시각화.
* 애니메이션 매직스트링 상수화 리팩터링.
* 설계 의도와 기술 선택 근거를 문서에 반영.

* **기술적 고려**
* 코드 변경 직후 문서 동기화를 기본 프로세스로 유지.
* 협업/면접 대응 관점에서 다이어그램 기반 설명력을 강화.

### **2026-02-06 (금): Hitbox 및 피격 시스템 구현**

* **오늘 반영한 작업**
* DamageCaster 기반 타격 판정 구현 (OverlapSphereNonAlloc).
* 애니메이션 이벤트 브리지(OnHitStart/OnHitEnd) 연동.
* IDamageable/Health 기반 데미지/사망 이벤트 흐름 구현.

* **기술적 고려**
* 프레임 루프에서 가비지 할당을 회피해 액션 구간 안정성을 확보.
* 공격자/피격자 결합도를 인터페이스로 낮춰 확장성을 확보.

### **2026-02-09 (월): Player DeadState 및 FSM 제네릭 통합**

* **오늘 반영한 작업**
* 플레이어 사망 처리(DeadState)와 입력 차단 흐름 구현.
* StateMachine<TState>, BaseState<TController>, IState 기반 제네릭 구조로 통합.
* PlayerVisual 통합 및 Null-safe 패턴 적용.

* **기술적 고려**
* 이벤트 기반 사망 전환으로 불필요한 폴링 비용을 제거.
* Player/Boss 공용 패턴으로 중복 코드를 축소.

### **2026-02-10 (화): Combat 구현 및 프로젝트 구조화**

* **오늘 반영한 작업**
* HitState/무적 시간/사망 코루틴 정리 흐름 보강.
* 보스 공격 실행기(BossAttackState) + IBossAttackPattern 전략 패턴 적용.
* 폴더/네임스페이스 구조 정리(Core 중심).

* **기술적 고려**
* 패턴 추가 시 기존 상태 코드 수정이 최소화되도록 OCP를 적용.
* 강제 상태 전환 시 히트박스 잔존을 차단해 유령 데미지 방지.

### **2026-02-11 (수): Dragon 에셋 마이그레이션 및 패턴 확장**

* **오늘 반영한 작업**
* Cube 보스를 Dragon 모델/애니메이터로 교체.
* ClawAttackPattern 추가 및 Feature Toggle 확장.
* 본 기반 콜라이더/피격 구조(BossHitBox) 정리.

* **기술적 고려**
* 모델 교체와 로직을 분리해 에셋 변경 비용을 낮춤.
* Primitive Collider 조합으로 성능/정확도 균형 확보.

### **2026-02-12 (목): 문서 정합성 점검 및 보스 패턴 폴리싱**

* **오늘 반영한 작업**
* 다이어그램/문서와 실제 코드 정합성 점검 및 수정.
* Bone-Synced Hitbox 정밀화 및 공격 이동 로직(MoveRaw) 분리.
* Claw 공격 타이밍/조기 종료 로직 폴리싱.

* **기술적 고려**
* 애니메이션 구간 기반 로직 제어로 비주얼-판정 동기화 정확도를 개선.
* 이동과 애니메이션 호출 분리로 상태 간 간섭을 최소화.

### **2026-02-20 (금): 보스 패턴 안정화, 환경 복구, 문서 통합 정리**

* **오늘 반영한 작업**
* Boss Combat 우선순위 정리: 공격 가능 시 AttackState 전환을 이동 호출보다 우선.
* AoE 안정화: 장판 지면 보정, 공중 연출 잠금, Fly 폴백, 예측 확산 파라미터 확장.
* 추적/복귀 지터 완화: 평면 거리 기준(XZ), chaseReengageBuffer 히스테리시스 적용.
* Projectile 종료 안정화: postFireRecoveryDuration, exitNormalizedTime 도입.
* 보스 2페이즈 전환: HealthRatio <= 0.5 단방향 전환 + 인트로 잠금 + 패턴 선택 안정화.
* Unity 2022 환경 복구: 패키지 manifest/lock 재정리, Rigidbody.velocity 호환 수정, Editor 어셈블리 앵커 추가.
* 문서 인코딩 가드레일 및 기술 문서 동기화 반영.

* **기술적 고려**
* 경계 구간 지터/조기 복귀 문제는 임계값 이중화와 종료 조건 결합으로 해결.
* 버전/패키지 불일치는 코드 이전에 환경 계층에서 정리해야 전체 시스템이 정상화됨.

### **2026-02-21 (토): 팀 환경 동기화 정책 및 플레이어 환경 오류 복구 가드 추가**

* **오늘 반영한 작업**
* free tier 기준 저장소 운영 정책 확정: 대용량 서드파티 에셋은 Git 제외, 수동 임포트 기준선 유지.
* PlayerAnimator 모션 참조 재연결(Hit, Attack1/2/3, Die) 및 점프 비활성 정책 유지(F10).
* Assets/Editor/PlayerAnimatorGuard.cs 확장:
  * 필수 상태/모션/파라미터 자동 검증.
  * Attack1/2/3의 OnHitStart/OnHitEnd 자동 보정 및 누락/순서 검증.
  * Tools/Validation/Fix Player Attack Events 메뉴 및 재임포트 훅 연결.
* 환경 세팅 복구 가이드 문서 작성: docs/blog/0221_Cross_Environment_Setup.md.
* 커밋 메시지 규칙 정리: summary/description 한국어 작성 + 커밋 전 사용자 확인.

* **기술적 고려**
* 환경 변경 시 가장 자주 깨지는 지점(Animator 참조/이벤트)을 에디터 자동 복구로 선제 차단.
* 에셋 배포와 프로젝트 동작 보장을 분리해 팀 동기화 비용을 낮춤.

## 📈 2월 마일스톤: 싱글플레이 로직 완성 (Capsule vs Cube)

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

#### 보스 행동 패턴 🔄
- [x] **패턴 1 (추적)**: 플레이어와의 거리 계산, 적정 거리 유지 및 공격 유도.
- [x] **근접 공격 (BasicAttackPattern)**: `IBossAttackPattern` Strategy Pattern 적용. 쿨다운 시스템.
- [x] **패턴 2 (돌격)**: 예고 표시 후 돌진.
- [ ] **패턴 3 (투사체)**: 큐브에서 작은 큐브(미사일) 발사.

---

### 3주차: 보스 행동 패턴 & 시스템 최적화 & 게임 루프 (The Logic)

#### 보스 행동 패턴 🔄
- [x] **패턴 3 (투사체)**: 큐브에서 작은 큐브(미사일) 발사 및 애셋 입히기.
- [x] **패턴 4 (장판)**: 하늘에서 땅을 향해 불 발사. 

#### 시스템 최적화 & 게임 루프 (The Logic)
- [x] **오브젝트 풀링**: 미사일/이펙트를 Zero-Allocation으로 관리.

---

### 4주차: 보스 행동 패턴 & 시스템 최적화 & 게임 루프 (The Logic)
#### 시스템 최적화 & 게임 루프 (The Logic)
- [ ] **UI 시스템**: HP 바, 보스 페이즈 알림 등 코드 제어.
- [ ] **게임 매니저**: 시작 → 전투 → 페이즈 전환 → 승리/패배 흐름 제어.
---

#### 🚧 폴리싱 
- [ ] **(플레이어)대쉬 방향 수정**: 공격 방향이 아닌 키보드 입력 방향으로 대쉬.
- [ ] **피격 플래시 이펙트**: `BaseVisual.FlashRoutine`을 Emission 기반으로 변경 (`material.SetColor("_EmissionColor")`) 또는 머티리얼 교체 방식 적용.
- [ ] **(보스)Run/Walk 애니매이션 추가**: Run 애니메이션 추가 및 그에 맞는 보스 움직임 속도 부드럽게 증가시키기
- [ ] **(플레이어)넉백 추가하기**: 플레이어가 보스에게 피격당했을 때 넉백 추가하기
- [ ] **다중 레이캐스트 탐지**: 눈 위치(몸통 1/2~머리 중간)에서 여러 방향 감지.
- [ ] **추적 알고리즘 검토**: NavMesh / A* 경로탐색 적용 여부 결정.
- [ ] **(보스)피격 중 공격 시전 유지**: 피격 애니메이션에 의해 공격 시전이 끊기지 않도록 하고, 데미지는 정상 반영.
- [ ] **(보스)패턴 3 투사체 조준 보정**: 플레이어가 드래곤 뒤쪽에 있어도 플레이어 방향으로 투사체 발사.
- [ ] **(보스)패턴별 동적 공격 사거리 적용**: 패턴 1은 근거리 사거리, 그 외 패턴은 더 긴 사거리 사용.


---

### 📝 공통 문서화 작업
- [ ] **책임(Responsibility) 문서화**: 로직별 책임 소재를 코드와 글로 명확히 설명 (면접 대비).
---


