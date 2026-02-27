# 🚀 Progress Log: Boss Raid Portfolio

## 🧩 로그 작성 규칙 (신규 엔트리부터 적용)

* 신규 엔트리는 `체크리스트 업데이트`와 `맥락노트`를 분리해서 작성한다.
* `기술적 고려`에는 아래 3항목을 고정으로 포함한다.
  * **무엇을 발견했는가**
  * **무엇을 수정했는가**
  * **왜 그렇게 판단했는가**
* 장기 작업 목록(마일스톤/버그/폴리싱)은 `docs/roadmap/Milestone_Backlog.md`에서 관리한다.
* 기존 과거 로그는 원형을 유지하고, 이 규칙은 신규 로그부터 적용한다.

### 기록 템플릿

* **오늘 반영한 작업**
* ...
* **체크리스트 업데이트**
* [x] ...
* [ ] ...
* **맥락노트**
* ...
* **기술적 고려**
* **무엇을 발견했는가**
* **무엇을 수정했는가**
* **왜 그렇게 판단했는가**

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

### **2026-02-23 (월): 전투 HUD 배치 완료 및 데미지 피드백 경로 연결**

* **오늘 반영한 작업**
* 전투 UI 레이아웃 배치 완료: 플레이어/보스 HP 바 영역과 고정형 데미지 텍스트 앵커를 화면에 배치하고 이름 라벨 슬롯(`Player`, `Dragon`) 정책을 확정.
* `CombatHUDController`에 `Health.OnDamageTaken`/`OnDeath` 이벤트 구독을 추가해 플레이어/보스 HP 바를 초기 동기화 + 즉시 갱신하도록 구현.
* `DamageCaster`에 공격 윈도우 종료 이벤트(`OnAttackWindowResolved`)를 추가해 적중 여부/누적 피해량을 외부로 전달.
* `PlayerController`에서 `DamageCaster` 이벤트를 구독해 HUD 피드백(`ShowDamageFeedback`)으로 연결하고, 시작 시 HUD 초기화(`Initialize`)와 이름 라벨(`Player`, `Dragon`) 세팅을 적용.
* 데미지 텍스트 정책 유지: 적중 시 `HIT + 피해량` 표시, 비적중 시 미표시.
* 고정형 데미지 텍스트 연출 보강: 적중 시 스케일 강조 후 짧은 페이드 아웃으로 자동 숨김 처리.
* HUD 표시 제어 경로 추가: 전투/연출 상황에서 전체 HUD On/Off가 가능하도록 `ShowHud(bool)` 토글 경로를 정리.
* 전투 판정 안정화: `DamageCaster`에 0 데미지 윈도우 차단, 상태 전환용 `ForceDisableHitbox()` 추가, `AttackState.Exit()`에서 히트박스 강제 종료 처리.
* 입력 엣지 보강: `MoveState` 공격 전환을 엣지 트리거로 변경해 게임 시작/포커스 클릭 시 의도치 않은 공격 진입을 완화.

* **기술적 고려**
* 매 프레임 폴링 대신 `Health`/`DamageCaster` 이벤트 기반으로 HUD를 갱신해 UI 동기화 경로를 단순화하고 불필요한 검사 비용을 줄임.
* HP 수치 문자열 갱신 대신 Fill 비율(`HealthRatio -> Image.fillAmount`) 중심으로 구성해 해상도/폰트 변화와 무관하게 시인성을 안정화.
* 공격 윈도우 단위 누적 피해량을 `DisableHitbox` 시점에 1회 발행해 콤보/다중 타격에서도 피드백 기준 시점을 일관되게 유지.
* 연출 코루틴은 이벤트 발생 시에만 실행되도록 제한해 평시 프레임 루프 부담을 늘리지 않도록 설계.
* `Health.TakeDamage`에서 0 이하 데미지를 무시하도록 가드해 피격 반응 이벤트 오염(무데미지 히트 반응)을 차단.

### **2026-02-24 (화): GameManager 구현 및 Progress_Log 규칙 정비**

* **오늘 반영한 작업**
* **GameManager 구현**
* 시작→전투 전환 경로 구현: `SceneLoader`를 추가해 목적지 씬 예약 후 `LoadingScene` 경유 전환 구조를 구성.
* 로딩 연출 분리: `LoadingSceneController`에서 비동기 로딩 진행률 UI, 최소 노출 시간, `allowSceneActivation` 타이밍 제어를 구현.
* 전투 종료 흐름 구현: `GameManager`에서 `Health.OnDeath` 이벤트 기반으로 승리/패배를 판정하고 결과 UI(`Victory` / `Defeated`)를 표시.
* 재시작 입력 연결: GameOver 상태에서 `Enter`(메인/넘패드) 입력으로 현재 씬 재시작 경로를 구현.
* 구현 범위 요약: 게임 루프의 시작/종료/재시작 핵심 경로를 코드로 연결.
* **문서 규칙 정비**
* 문서 표기 규칙 정비: `AI_Maintenance_Guide` 기준에 맞춰 `Progress_Log`의 버그/폴리싱 체크리스트를 우선순위(`🔴 1순위`, `🟡 2순위`, `🟢 3순위`)와 작업 태그(`(플레이어)`, `(보스)`, `(플레이어, UI)`) 형식으로 통일.
* 반영 범위 요약: 버그/폴리싱 항목을 우선순위 + 태그 기준으로 정렬해 어떤 파트 작업인지 빠르게 식별 가능하도록 정리.

* **기술적 고려**
* 게임 루프 컴포넌트를 전환(`SceneLoader`), 로딩 연출(`LoadingSceneController`), 결과 판정(`GameManager`)으로 분리해 변경 영향을 국소화.
* 게임오버 판정은 사망 이벤트 수집 후 1회 확정 플래그로 처리해 중복 결과 처리/중복 UI 노출을 방지.
* 문서 측면에서는 우선순위 마커와 작업 태그를 고정 규칙으로 두어 버그/폴리싱 백로그의 정렬 기준을 명시.

### **2026-02-26 (목): TitleScene 진입 경로, 결과 검증, 보스 감지 정책 정리**

* **오늘 반영한 작업**
* **TitleScene 진입 경로 연결**
* `TitleScene` 하이어라키를 기준으로 `TitleFlow` 오브젝트를 추가하고 시작 입력 진입 지점을 분리.
* `TitleSceneController`를 신규 구현해 `Input.anyKeyDown` 입력 시 `SceneLoader.Load(GameSceneId.GamePlay)`를 호출하도록 연결.
* 오입력 방지를 위해 `inputLockDuration`(기본 0.1초) 가드를 두고, 첫 입력 이후 중복 전환을 차단.
* Build Settings 씬 순서를 `TitleScene -> LoadingScene -> GamePlayScene`으로 확정.
* **GameManager 동시 사망 결과 검증**
* `GamePlayScene_TestResult` 테스트 씬을 추가해 결과 판정 전용 검증 환경을 분리.
* 플레이어/보스 `Health`를 1로 세팅하고 `SimultaneousDeathTest`(`Assets/Scripts/Test/SimultaneousDeathTest.cs`)로 동일 프레임 사망 트리거를 구성.
* `K` 입력으로 플레이어/보스를 같은 프레임에 동시에 사망시키는 시나리오를 반복 검증.
* `GameManager` 결과 UI가 `Victory`로 출력되는 것을 확인하고, GameOver 상태에서 `Enter` 재시작 입력이 정상 동작하는지 확인.
* **보스 감지/스크림 정책 정리**
* `BossIdleState`, `BossSearchingState`의 Combat 전환 조건을 `IsTargetInDetectionRange()`(수평 거리 기준)로 통일해, 플레이어가 걷기 진입해도 스크림(페이즈 인트로)이 즉시 발동되도록 수정.
* `BossController`에 `IsTargetInDetectionRange()` 헬퍼를 추가해 감지 반경 판정을 중앙화.
* 최종적으로 미사용 `CheckLineOfSight()` 메서드를 제거하고, 감지 설정의 `obstacleMask` 및 `OnDrawGizmosSelected()` LOS Linecast 디버그 라인을 제거.
* 보스 감지 정책을 장애물/시야선과 무관한 거리 단일 감지 규칙으로 확정.
* **보스 패턴별 공격 사거리 분리**
* `BossController`의 단일 `attackRange`를 패턴별 인스펙터 값(`basic`, `lunge`, `projectile`, `aoe`)으로 분리하고, 기존 직렬화 값 호환을 위해 `FormerlySerializedAs("attackRange")`를 `basicAttackRange`에 연결.
* `BossCombatState` 패턴 선택 시 현재 거리에서 유효한 패턴만 후보에 포함하도록 수정해, 패턴 1은 근거리, 나머지 패턴은 더 긴 사거리 규칙을 반영.
* 추적 히스테리시스 기준을 단일 임계값이 아닌 "현재 페이즈 활성 패턴 중 최대 사거리 + chaseReengageBuffer"로 갱신.
* `AoEAttackPattern`의 타겟 미존재 보조 중심점 계산도 `AoEAttackRange`를 사용하도록 정리.
* `System_Blueprint`, `Technical_Glossary` 문서를 최신 정책 기준으로 동기화.

* **기술적 고려**
* 타이틀 입력 수집 책임을 `TitleSceneController`로 분리해 `SceneLoader`/`LoadingSceneController`의 기존 책임(전환 관리/비동기 로딩)을 유지.
* 타이틀 진입 시 `SceneLoader.CancelPendingTransition()`으로 정적 전환 상태를 초기화해 이전 플레이 세션 잔존 상태를 방지.
* 현재 `GameManager.LateUpdate()`는 `_bossDead`를 우선 검사하므로 동시 사망 시 `Victory`가 확정된다.
* 실플레이에서는 애니메이션 이벤트/`FixedUpdate` 타이밍으로 프레임 차이가 생길 수 있어, 동일 프레임 강제 테스트와 일반 전투 테스트를 분리해 검증해야 한다.
* 감지 정책을 단순화하면 레이어/콜라이더 구성에 따른 오탐·미탐 변동이 줄고, QA 재현성이 높아진다.
* 반대로 플레이어가 장애물 뒤에 숨는 스텔스 플레이는 의도적으로 비지원이 되므로, 추후 기획 변경 시 감지 계층(거리 + 시야선)을 다시 설계해야 한다.
* 공격 진입 가능 여부를 패턴별 사거리로 분리하면, 페이즈/거리 조합에 따라 "실행 가능한 패턴 집합"이 명확해져 밸런싱 조정과 QA 회귀 확인이 쉬워진다.
* 추적 해제 임계값을 현재 페이즈의 최대 공격 사거리로 계산하면, 근접/원거리 패턴이 혼재한 상태에서도 경계 왕복 지터를 줄이면서 원거리 패턴 선진입을 자연스럽게 허용할 수 있다.

### **2026-02-27 (금): AI 문서 워크플로우 체계화 (Index + 상세 챕터 분리)**

* **오늘 반영한 작업**
* `AI_Maintenance_Guide.md`를 인덱스 중심 구조로 재작성하고, 능동 Hook 규칙/Plan-First/승인 게이트/Agent Roles를 요약 규칙으로 상향.
* `docs/maintenance/` 챕터 문서 3종을 신설해 상세 운영 규칙(Hook & Plan-First, 실행·역할·품질보고, 다이어그램·인코딩·로그 표기)을 분리.
* `Progress_Log.md`에 체크리스트 업데이트/맥락노트 분리 규칙과 품질 보고 3항목 템플릿을 추가.
* `Progress_Log.md`에서 마일스톤/버그/폴리싱 블록을 분리하고, 장기 백로그는 `docs/roadmap/Milestone_Backlog.md`로 이관.
* `System_Blueprint.md`의 Prompting Guide를 계획서 우선 프로세스로 보강하고, 승인 전 구현 금지 규칙을 명시.
* `Technical_Glossary.md`에 신규 운영 용어(Hook Rule, Plan-First Gate, Approval Gate, Quality Report Triple 등)를 등록.
* 웹뷰 URL 링크 이슈를 반영해, 문서 규칙에 VS Code 로컬 경로 링크 강제(`file+.vscode-resource...` 금지)를 명시.

* **체크리스트 업데이트**
* [x] AI 가이드 인덱스화 및 상세 챕터 분리 완료
* [x] Plan-First 승인 게이트 문서화 완료
* [x] Progress_Log 신규 기록 포맷(체크리스트/맥락노트 분리) 반영
* [x] System_Blueprint Prompting Guide 보강
* [x] Technical_Glossary 신규 용어 동기화
* [x] 링크 표기 규칙 보강(VS Code 로컬 경로 강제, 웹뷰 URL 금지)

* **맥락노트**
* 단일 대형 매뉴얼은 탐색 비용이 커져 문서 조회 단계에서 토큰 낭비가 발생하기 쉬우므로, 인덱스 + 상세 챕터 구조로 분리해 참조 경로를 고정했다.
* AI가 구현에 바로 진입하지 않도록 `Hook 판정 -> 계획서 -> 승인` 3단 게이트를 상위 가이드에 명시해, 실행 순서를 문서 차원에서 강제했다.

* **기술적 고려**
* **무엇을 발견했는가**
* 기존 가이드는 실행 규칙이 단일 본문에 섞여 있어, 작업 유형별 선행 문서 판단과 완료 보고 품질 기준이 명시적으로 분리되지 않았다.
* **무엇을 수정했는가**
* 인덱스 문서에서 핵심 규칙만 유지하고, 세부 규칙은 챕터 문서로 분리했다. 동시에 Progress Log 기록 포맷을 구조화해 체크리스트와 맥락 기록을 분리했다. 또한 링크 표기 규칙을 로컬 경로 중심으로 고정했다.
* **왜 그렇게 판단했는가**
* 문서 접근 단계의 의사결정(무엇을 먼저 읽을지, 언제 구현 가능한지, 어떻게 완료를 보고할지)을 표준화하면 작업 일관성과 회귀 추적성이 높아지고, 긴 대화에서도 맥락 손실이 줄어든다.

## 🗂️ Milestone Backlog

마일스톤/버그/폴리싱 백로그는 아래 문서에서 관리한다.

- [Milestone_Backlog.md](./roadmap/Milestone_Backlog.md)


