# 🚀 Progress Log: Boss Raid Portfolio

## 🧩 로그 작성 규칙 (신규 엔트리부터 적용)

* 신규 엔트리는 `체크리스트 업데이트`와 `맥락노트`를 분리해서 작성한다.
* `기술적 고려`에는 아래 3항목을 고정으로 포함한다.
  * **무엇을 발견했는가**
  * **무엇을 수정했는가**
  * **왜 그렇게 판단했는가**
* 동일 날짜의 로그 엔트리는 1개만 유지한다.
* 같은 날의 추가 작업은 새 날짜 헤더를 만들지 않고, 기존 날짜 엔트리 내부 소제목/항목으로 병합한다.
* 코드 변경이 포함된 로그는 `코드 검사 결과` 블록(명령/결과/미실행 사유)을 반드시 포함한다.
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
* **코드 검사 결과 (코드 변경 시 필수)**
* 명령: `dotnet build Assembly-CSharp.csproj -v:minimal`
* 결과: 성공/실패 (Warning N, Error N)
* 미실행 사유 및 대체 검증: (미실행 시 필수)
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

### **2026-02-27 (금): AI 문서 워크플로우 체계화 (Index + 상세 챕터 분리) & 플레이어 거리에 따른 페이즈1 공격 패턴 수정**

#### **AI 문서 워크플로우 체계화 (Index + 상세 챕터 분리)**

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

#### **플레이어 거리에 따른 페이즈1 공격 패턴 수정**

* **오늘 반영한 작업**
* 페이즈1 공격 패턴 통합 구현 계획을 확정하고 로그에 반영했다.
* Basic 판정 기준점을 Boss Root가 아닌 Head Empty Transform으로 분리하기로 결정했다.
* `BossController`에 `basicAttackRangeOrigin` SerializeField와 `GetPlanarDistanceFromBasicAttackOriginToTarget()` 헬퍼를 추가했다.
* Basic 사거리 Gizmo 기준점도 `basicAttackRangeOrigin`으로 맞췄다(미할당 시 Boss Root 폴백).
* `GamePlayScene`, `GamePlayScene_TestResult`의 Boss 인스펙터에서 `basicAttackRangeOrigin`을 `HeadDamageCasterPlace`로 연결했다.
* `BossFSM` 페이즈1 패턴 선택을 교대 로직에서 거리 우선 로직으로 변경했다: Basic/Lunge를 동시에 만족하면 Basic을 우선하고, Basic 범위를 벗어나면서 Lunge 범위를 만족할 때만 Lunge를 선택한다.
* `BossController`에서 `basicAttackRange`가 바뀌면 `HeadDamageCaster.radius`가 동일 값으로 유지되도록 동기화 로직(`SyncBasicAttackRangeToHeadDamageCaster`)을 적용했다.
* 적용 순서를 `BossController -> Scene 참조 연결 -> BossFSM -> 검증 -> 문서 동기화`로 고정했다.

* **체크리스트 업데이트**
* [x] 통합 계획 합의 및 Progress_Log 반영 완료
* [x] `BossController`에 `basicAttackRangeOrigin` SerializeField 및 거리 헬퍼 추가
* [x] `GamePlayScene`/`GamePlayScene_TestResult`에 Head Empty 참조 연결
* [x] `BossFSM` 페이즈1 패턴 선택 로직을 거리 기준 분기로 교체
* [x] `basicAttackRange`와 `HeadDamageCaster.radius` 동기화 적용
* [x] 구현 완료 후 `Progress_Log`/`System_Blueprint`/`Technical_Glossary` 동기화

* **맥락노트**
* 기존 Basic 사거리 기준점이 Boss Root라 측면/후방 의도 제어가 어려워, Head 기준점 분리가 선행되어야 한다.
* `HeadDamageCasterPlace`를 기준점으로 고정해 기획 의도(머리 기준 Basic 거리 판정)를 씬 설정과 코드에서 동일하게 유지했다.
* 페이즈1 중첩 구간(Basic/Lunge 동시 충족)에서는 근접 패턴 의도를 보장하기 위해 Basic 고정 우선순위를 적용했다.

* **코드 검사 결과 (코드 변경 시 필수)**
* 명령: `dotnet build Assembly-CSharp.csproj -v:minimal`
* 결과: 성공 (Warning 3, Error 0)
* 미실행 사유 및 대체 검증: 해당 없음

* **기술적 고려**
* **무엇을 발견했는가**
* 페이즈1 패턴 선택이 교대 선택 기반이라 중첩 구간에서 Lunge가 선택될 수 있었고, 이는 근접 의도와 충돌했다.
* **무엇을 수정했는가**
* Basic 거리 기준을 `HeadDamageCasterPlace` 기반으로 분리하고, `BossFSM`에서 Basic/Lunge 동시 만족 시 Basic 우선 규칙으로 전환했다. 또한 `basicAttackRange`와 `HeadDamageCaster.radius`를 자동 동기화해 판정 반경과 사거리 설정이 항상 일치하도록 보정했다. 관련 규칙을 `System_Blueprint`, `Technical_Glossary`에 동기화했다.
* **왜 그렇게 판단했는가**
* 중첩 구간 우선순위를 고정하면 패턴 선택이 거리 의도와 일치하고, 이후 각도 기반 Lunge 조건을 추가해도 기준점/우선순위 구조를 유지한 채 확장할 수 있다.

#### **BossController 필드 선언 우선 리팩토링**

* **오늘 반영한 작업**
* `BossController`의 `OnValidate()`를 필드 선언 중간에서 필드 선언부 하단으로 이동했다.
* 멤버 배치를 `필드 선언 -> 메서드` 구조로 정렬해 선언부와 실행부를 분리했다.
* 코드 변경 후 `dotnet build Assembly-CSharp.csproj -v:minimal`을 재실행해 컴파일 성공을 확인했다.
* `System_Blueprint`, `Technical_Glossary`에 멤버 배치 규칙을 동기화했다.

* **체크리스트 업데이트**
* [x] `OnValidate()` 위치를 필드 선언 완료 이후로 정렬
* [x] 빌드 검사 재실행 및 결과 기록
* [x] `Progress_Log`/`System_Blueprint`/`Technical_Glossary` 동기화 반영

* **맥락노트**
* `OnValidate`가 필드 선언 사이에 있으면 인스펙터 노출값과 검증 로직의 대응 관계를 빠르게 읽기 어렵다.
* 동작 변화 없는 구조 리팩토링이라도 배치 기준을 명확히 두는 편이 이후 유지보수에 유리하다.

* **코드 검사 결과 (코드 변경 시 필수)**
* 명령: `dotnet build Assembly-CSharp.csproj -v:minimal`
* 결과: 성공 (Warning 3, Error 0)
* 미실행 사유 및 대체 검증: 해당 없음

* **기술적 고려**
* **무엇을 발견했는가**
* `OnValidate`가 필드 블록 중간에 위치해 선언부 스캔과 초기 설정 검토 흐름이 끊겼다.
* **무엇을 수정했는가**
* `OnValidate`를 모든 필드 선언 뒤로 이동해 선언부와 실행부를 분리했다.
* **왜 그렇게 판단했는가**
* 동작 변화 없이 코드 읽기 흐름을 단순화할 수 있고, 이후 필드 추가 시 배치 기준을 일관되게 유지할 수 있다.

#### **(보스) attack2 도약 회귀 수정 (루트모션 브리지)**

* **오늘 반영한 작업**
* `LungeAttackPattern`에서 `rushPhaseRatio`/`MoveRaw` 기반 전진 로직을 제거하고, `exitPhaseRatio` 기준 종료만 유지했다.
* `LungeAttackPattern.Enter/Exit`에 `SetLungeRootMotionEnabled(true/false)`를 연결해 도약 구간에서만 루트모션을 활성화했다.
* `BossVisual`에 루트모션 릴레이 구성(`ResolveRootMotionRelay`)과 토글 API(`SetLungeRootMotionEnabled`)를 추가했다.
* Animator 오브젝트에서 `OnAnimatorMove`를 수신하는 `BossRootMotionRelay`를 `BossVisual.cs` 내부 클래스로 추가해 `animator.deltaPosition`을 보스 루트로 전달했다.
* `BossController`에 `ApplyLungeRootMotion(Vector3)`를 추가해 루트모션 델타(XZ)를 `CharacterController.Move`로 적용하도록 구성했다.
* `LungeAttackSettings`에서 더 이상 사용하지 않는 `rushSpeed`, `rushPhaseRatio` 직렬화 필드를 제거했다.
* Lunge 종료 시점은 인스펙터 노출값 대신 코드 상수 `0.8`로 고정하고(`FixedExitPhaseRatio`), `exitPhaseRatio` 직렬화 필드를 제거했다.
* Lunge 시작 프레임에 타겟 정면으로 즉시 정렬하도록 `RotateTowardsImmediate`를 추가하고, `LungeAttackPattern.Enter`에서 사용하도록 연결했다.
* 플레이 요구사항 변경에 따라 Lunge 타겟 유도/거리 클램프 로직을 제거하고, `ApplyLungeRootMotion`이 애니메이션 루트모션 델타를 그대로 부모 루트로 전달하도록 단순화했다.
* 1번 이슈 해결: Lunge 시작 시 타겟 방향을 고정(`BeginLungeTravelDirectionLock`)하고, 루트모션은 델타 크기만 유지한 채 고정 방향으로 적용해 플레이어 방향 도약을 안정화했다.
* **현재 확인된 문제점**
* [x] 1. Lunge가 플레이어 방향으로 정확히 뛰지 않음 (고정 방향 도약 적용으로 해결)
* [x] 2. Hierarchy 상 `Boss` 부모 루트와 자식 `Visual` 이동 불일치가 간헐적으로 관찰됨 (루트모션 폴백 + 로컬 기준점 복원으로 해결)

* **체크리스트 업데이트**
* [x] `LungeAttackPattern`의 `rushPhaseRatio` 분기 삭제
* [x] 도약 전용 루트모션 토글 API 추가
* [x] Animator `OnAnimatorMove` -> Boss 루트 이동 전달 경로 구성
* [x] `LungeAttackSettings` 미사용 필드 정리
* [x] Lunge 종료 시점 `0.8` 고정 및 인스펙터 노출 제거
* [x] Lunge 시작 즉시 정면 정렬(회전 지연 제거)
* [x] Lunge 루트모션 타겟 유도/거리 클램프 제거 (애니메이션 델타 직결)
* [x] Lunge 시작 방향 고정(플레이어 방향 도약 보정)
* [x] 코드 검사 및 문서 동기화 반영

* **맥락노트**
* 현재 보스 구조는 `BossController`(부모)와 Animator가 붙은 드래곤 모델(자식)이 분리되어 있어, 애니메이션만 재생하면 자식 비주얼만 이동하고 부모 루트는 제자리에 남았다.
* 루트모션을 부모 `CharacterController`로 전달하지 않으면 도약 착지 이후 FSM/물리 기준 위치가 원래 자리로 유지되어 복귀처럼 보이는 문제가 반복된다.
* `OnAnimatorMove`는 Animator가 붙은 오브젝트에서 수신해야 하므로, 별도 스크립트 파일 추가 대신 `BossVisual.cs` 내부 릴레이 클래스로 구성해 현재 csproj 기준 빌드 누락 이슈를 회피했다.
* 루트모션 복구 이후 보정 로직(타겟 유도/클램프)을 추가했으나, 실제 플레이에서는 클립 고유 궤적을 훼손해 이동 방향이 어색해지는 회귀가 발생했다.

* **코드 검사 결과 (코드 변경 시 필수)**
* 명령: `dotnet build Assembly-CSharp.csproj -v:minimal`
* 결과: 성공 (Warning 3, Error 0)
* 미실행 사유 및 대체 검증: 해당 없음

* **기술적 고려**
* **무엇을 발견했는가**
* 도약 애니메이션은 자식 모델의 루트 본/콜라이더를 전진시키지만, 부모 `BossController` 루트 Transform은 별도로 이동하지 않아 도약 착지 위치가 게임플레이 좌표에 반영되지 않았다.
* 타겟 유도/클램프 보정은 루트모션 원본 궤적을 왜곡해, 플레이어 위치와 상대 배치에 따라 비정상적인 방향 오차를 유발할 수 있었다.
* 보정 제거 후에는 클립 원본 궤적이 유지되지만, 시작 방향 정렬만으로는 일부 전투 상황에서 플레이어 방향 도약이 불안정할 수 있었다.
* **무엇을 수정했는가**
* 도약 이동 소스를 수동 `MoveRaw`에서 애니메이션 루트모션으로 전환했고, `OnAnimatorMove -> ApplyLungeRootMotion` 브리지로 부모 이동을 일치시켰다. 동시에 `rushSpeed/rushPhaseRatio` 의존성을 제거하고, 종료 시점도 인스펙터 값이 아닌 고정 상수 `0.8`로 통일해 설정과 실제 동작 불일치를 해소했다.
* Lunge 진입 시 즉시 정면 정렬은 유지하되, 타겟 유도/거리 클램프 로직을 제거하고 `ApplyLungeRootMotion`이 애니메이션 델타(XZ)를 그대로 적용하도록 단순화했다.
* 1번 이슈 대응으로 Lunge 시작 시 타겟 방향을 고정해 두고(`BeginLungeTravelDirectionLock`), 실제 이동은 루트모션 델타의 크기만 사용해 고정 방향으로 적용하도록 변경했다.
* **왜 그렇게 판단했는가**
* 도약 위치를 애니메이션 클립의 전진량과 1:1로 맞추면 착지 지점이 자연스럽고, 복귀 모션을 중간 종료(`exitPhaseRatio`)해도 부모 루트가 착지 지점에 고정되어 전투 흐름이 끊기지 않는다.
* 현재 요구사항은 "플레이어 정렬 우선"이 아니라 "애니메이션 끝지점 일치"이므로, 보정 없이 원본 루트모션을 그대로 전달하는 구현이 의도와 정확히 맞다.
* 방향만 고정하면 애니메이션 이동 거리/템포는 보존하면서도 도약 방향을 플레이어 쪽으로 안정화할 수 있어 1번 문제를 독립적으로 해결할 수 있다.

#### **(보스) attack2 부모/자식 루트 동기화 보강 (2번 이슈 해결)**

* **오늘 반영한 작업**
* `BossRootMotionRelay`에 `animator.deltaPosition`이 0 또는 미소값일 때, 자식 `Visual`의 실제 월드 이동량을 폴백으로 적용하는 경로를 추가했다.
* Lunge 활성화 시점의 `Visual` 로컬 기준 포즈를 캐시하고, `OnAnimatorMove` 및 Lunge 종료 시점에 로컬 기준점으로 복원해 부모/자식 좌표 오프셋 누적을 차단했다.
* 루트모션 디버그 로그를 `animDelta`, `appliedDelta`, `fallback`, `visualLocalOffset` 항목으로 확장해 원인 분기 정보를 확인할 수 있도록 보강했다.
* 기존 체크리스트의 2번 이슈(`Boss` 부모 루트-자식 `Visual` 이동 불일치)를 해결 처리했다.

* **체크리스트 업데이트**
* [x] `BossRootMotionRelay` 델타 0 프레임 폴백 경로 추가
* [x] Lunge 도중 `Visual` 로컬 기준점 고정/복원 적용
* [x] 루트모션 디버그 로그 분기 항목 확장
* [x] `Hierarchy 상 Boss 루트-Visual 불일치` 이슈 해결 처리

* **맥락노트**
* 애니메이션/임포트 설정 조합에 따라 일부 프레임에서 `animator.deltaPosition`이 0에 수렴할 수 있어, 델타값 단일 소스만 사용하면 부모 루트 이동이 누락될 수 있다.
* 자식 `Visual`의 로컬 오프셋이 누적되면 전투 판정의 기준 좌표(부모)와 렌더링 좌표(자식)가 분리되어, "자식만 움직이는 것처럼 보이는" 간헐적 불일치가 재발한다.
* 이동량 계산을 `animator.deltaPosition` 우선 + `Visual world delta` 폴백의 이중 경로로 구성하고, 로컬 기준점 복원을 함께 적용해 좌표 일관성을 우선 보장했다.

* **코드 검사 결과 (코드 변경 시 필수)**
* 명령: `dotnet build Assembly-CSharp.csproj -v:minimal`
* 결과: 성공 (Warning 3, Error 0)
* 미실행 사유 및 대체 검증: 해당 없음

* **기술적 고려**
* **무엇을 발견했는가**
* `animator.deltaPosition`이 항상 유효하다고 가정하면, 특정 클립/임포트 조건에서 부모 루트 이동 누락이 발생할 수 있었다.
* 루트모션 적용 중 자식 로컬 기준점이 유지되지 않으면 부모/자식 좌표계 불일치가 누적될 수 있었다.
* **무엇을 수정했는가**
* `BossRootMotionRelay`에서 적용 델타 계산을 이중화(`animDelta` 우선, 필요 시 `Visual world delta` 폴백)했고, Lunge 구간에 한해 자식 로컬 포즈 캐시/복원 로직을 추가했다.
* 디버그 로그를 확장해 실제 적용 델타와 폴백 여부, 자식 로컬 오프셋을 동시 추적 가능하게 정리했다.
* **왜 그렇게 판단했는가**
* 입력 데이터 품질(델타 유효성)이 흔들리는 프레임에서도 부모 루트 이동을 보장하려면 단일 데이터 소스 의존을 줄여야 한다.
* 부모/자식 좌표 정합성은 공격 판정, 추적, 상태 전환 전체 안정성에 직접 연결되므로, 로컬 기준점 복원을 포함한 방어적 동기화가 필요했다.

#### **(보스) attack2 판정 종료/상태 종료 시점 분리**

* **오늘 반영한 작업**
* `LungeAttackPattern`의 종료 타이밍을 단일 기준에서 분리했다.
* 히트박스 종료는 `normalizedTime >= 0.8`에서 수행하고, 공격 상태 복귀는 `normalizedTime >= 1.0`에서 수행하도록 변경했다.
* Lunge 상태가 끝난 직후 프레임에서도 안전하게 종료되도록 `Lunge 상태 진입 여부 + 히트박스 종료 여부`를 기준으로 보조 종료 조건을 추가했다.

* **체크리스트 업데이트**
* [x] Lunge 히트박스 종료 시점 고정(0.8)
* [x] Lunge 상태 종료(Combat 복귀) 시점 고정(1.0)
* [x] Lunge 상태 이탈 프레임 안전 종료 조건 추가

* **맥락노트**
* 기존에는 `0.8`에서 상태 자체를 종료해 애니메이션 끝지점 전 복귀가 발생했다.
* 이번 변경으로 도약 이동은 클립 끝까지 유지하면서도, 타격 판정은 의도한 시점(0.8)에서 먼저 종료할 수 있게 분리했다.
* 실측 로그에서 `visualLocalOffset=(0,0,0)`이 유지되고 `fallback=False`가 지속된 것을 통해 부모/자식 동기화 보강은 유효함을 확인했다.
* 같은 로그에서 `Exit` 시점이 `nTime=0.795`로 확인되어, "애니메이션 끝지점 정지" 요구를 충족하려면 상태 종료 시점을 1.0으로 분리해야 함을 확인했다.

* **코드 검사 결과 (코드 변경 시 필수)**
* 명령: `dotnet build Assembly-CSharp.csproj -v:minimal`
* 결과: 성공 (Warning 3, Error 0)
* 미실행 사유 및 대체 검증: 해당 없음

* **기술적 고려**
* **무엇을 발견했는가**
* Lunge 종료 시점과 히트박스 종료 시점을 같은 기준값으로 묶어두면, 타격 밸런싱과 이동 연출을 독립적으로 조정하기 어렵다.
* **무엇을 수정했는가**
* 종료 판정을 `히트박스 종료(0.8)`와 `상태 종료(1.0)`로 분리하고, 애니메이터 상태 전환 경계 프레임에서의 정지 위험을 방지하는 보조 종료 조건을 추가했다.
* **왜 그렇게 판단했는가**
* 이동 연출 완결성과 전투 판정 타이밍을 분리해야, "끝지점까지 이동" 요구와 "과도한 후반 판정 방지" 요구를 동시에 만족할 수 있다.

* **검증 로그 판독 기준**
* `LungeDebug][RootMotion`: `visualLocalOffset=(0,0,0)` 유지 여부로 부모/자식 좌표 정합성 확인
* `LungeDebug][RootMotion`: `fallback=True/False` 빈도로 루트모션 소스 안정성 확인
* `LungeDebug][Exit`: `normalizedTime`이 1.0 근처/이상인지 확인하여 애니메이션 끝지점 종료 여부 판단
* 전투 로그(`Player took damage`): `normalizedTime 0.8` 이후 추가 타격 발생 여부로 히트박스 종료 타이밍 확인

## 🗂️ Milestone Backlog

마일스톤/버그/폴리싱 백로그는 아래 문서에서 관리한다.

- [Milestone_Backlog.md](./roadmap/Milestone_Backlog.md)


