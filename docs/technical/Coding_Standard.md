# 📜 Coding Standard: Boss Raid Portfolio

이 문서는 프로젝트의 성능 최적화(Zero-GC), 유지보수성, 네트워크 확장성을 위한 코드 작성 규칙을 정의합니다.

## 1. Memory Management & Performance (Zero-GC)

* **No Allocation in Update**: `Update`, `FixedUpdate`, `LateUpdate` 등 매 프레임 호출되는 메서드 내에서 `new` 키워드 사용(객체 생성)을 엄격히 금지한다.
* **Struct over Class**: 네트워크 패킷이나 단순 데이터 전달자(DTO)는 스택 메모리를 사용하는 `struct`로 정의한다. (예: `PlayerInputPacket`)
* **Non-Alloc Physics API**: 물리 쿼리 시 반드시 `NonAlloc` 버전의 API를 사용한다.
* ❌ `Physics.OverlapSphere()`
* ✅ `Physics.OverlapSphereNonAlloc(position, radius, results)`


* **Collection Pre-allocation**: 물리 판정 결과나 객체 리스트를 담는 배열/리스트는 `Awake`에서 미리 최대 크기로 할당하여 재사용한다.
* **Pooling Lifecycle Rule**: 투사체/이펙트는 `Object Pool`로 관리하며, 런타임 `Instantiate/Destroy`를 금지한다. 활성화 시 `Initialize`, 종료 시 반드시 `ReturnToPool` 경로로만 반납한다.
* **Pooled VFX Event Rule**: 풀링 투사체 VFX는 이벤트 계약을 고정한다. 스폰 시 `create`, 피격 시 `hit`, 반납 시 `stop`을 사용하며, 피격 연출이 필요한 프리팹은 콜라이더를 비활성화한 뒤 `hitReturnDelay` 후 반납한다.
* **AoE Telegraph Material Rule**: AoE 경고 표시 갱신 시 `Renderer.material` 접근으로 런타임 머티리얼 복제를 만들지 않는다. 반드시 `MaterialPropertyBlock`으로 Fill/Alpha를 제어한다.
* **AoE Query Buffer Rule**: AoE 데미지 판정은 `Physics.OverlapSphereNonAlloc`을 사용하고, 충돌 버퍼(`Collider[]`)는 장판 컨트롤러 또는 패턴 컨텍스트에서 선할당 후 재사용한다.

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
* **Projectile Axis Separation**: 투사체 유도는 축을 분리한다. 수평 조향은 XZ 평면(`RotateTowards`)으로 처리하고, 수직 추적은 Y축 `MoveTowards`로 별도 처리한다.
* **Projectile Vertical Follow Tuning**: Y축 추적 강도는 하드코딩하지 않고 인스펙터 직렬화 값(`verticalFollowSpeed`)으로 노출한다. `0`일 때는 발사 높이를 유지해야 한다.
* **Boss Distance Metric Rule**: Boss의 감지/추적/공격 거리 비교는 `Vector3.Distance` 대신 Y축을 제거한 평면 거리(XZ)를 기준으로 처리한다.
* **Boss Chase Hysteresis Rule**: 공격 사거리 경계에서 `Move`/`Idle` 왕복이 발생하지 않도록, 현재 페이즈에서 활성화된 패턴의 `최대 사거리`(해제)와 `최대 사거리 + chaseReengageBuffer`(재진입) 이중 임계값을 사용한다.
* **Hit Resolution Robustness**: 투사체 충돌 처리 시 `OnTriggerEnter`만 가정하지 않는다. `OnCollisionEnter` 경로를 함께 제공하고, 데미지 대상 탐색은 `GetComponent<IDamageable>()` 후 `GetComponentInParent<IDamageable>()` 순으로 폴백한다.
* **AoE Ground Validation**: 장판 스폰 좌표는 지면 Raycast로 확정하고, 경기장 경계 밖 좌표는 생성 전에 보정(Clamp)하거나 스킵한다.
* **AoE Tick Determinism**: 장판 틱 데미지는 프레임마다 즉시 호출하지 않고 누적 타이머(`tickInterval`) 기준으로 처리한다.



## 5. Boss Pattern 4 (AoE) Rule Set

* **Airborne Phase Contract**: AoE 패턴 중 공중 연출은 `takeOff -> FlyForward -> FlyIdle -> Land` 순서를 기본 계약으로 둔다.
* **Animation Fallback Rule**: AoE 비행 상태가 Animator에 없으면 `Locomotion`/`Idle` 계열로 폴백하여 Null/멈춤 상태를 방지한다.
* **Damage Ownership Rule**: 장판 데미지는 보스 Owner `InstanceID`를 포함해 자기 자신 피격을 방지하고, 필요 시 동일 틱 중복 타격 제한을 둔다.
* **Shared Projectile Pool Rule**: AoE는 별도 투사체 풀을 만들지 않고, `ProjectileAttackPattern`과 동일한 `BossProjectilePool` 및 fire prefab을 재사용한다.
* **Impact Timing Sync Rule**: 플레이어 피해가 시작되는 시점은 fire prefab 착지 시점과 동일해야 한다. 기본 동기화 규칙은 `telegraphDuration == projectile impactTime`.
* **Editor-Tunable First**: `radius`, `telegraphDuration`, `activeDuration`, `tickInterval`, `damage`는 하드코딩하지 않고 직렬화 설정으로 노출한다.
* **Predictive Spread Rule**: 장판 분포는 타겟 현재 위치만 쓰지 않고 진행 방향 예측을 적용한다. 최소 `headingLeadTime`, `maxHeadingLeadDistance`, `forwardSpreadRadius`, `sideSpreadRadius`, `headingBias`, `headingMinSpeed`를 인스펙터에서 조정 가능해야 한다.
* **Fallback Disc Grounding Rule**: `AoECircleController`의 런타임 폴백 디스크 오프셋(`fallbackYOffset`) 기본값은 `0`을 유지해 바닥 밀착을 보장한다.
* **Projectile Exit Sync Rule**: Projectile 패턴 종료는 마지막 발사 수만으로 즉시 반환하지 않고 `postFireRecoveryDuration` 및 `exitNormalizedTime` 조건을 함께 만족할 때 복귀한다.

## 6. Naming & Style Conventions

* **Fields**: private 필드는 `_camelCase` 형식을 사용한다. (예: `_moveSpeed`)
* **Properties**: public 프로퍼티는 `PascalCase`를 사용한다. (예: `CurrentState`)
* **Methods**: 모든 메서드는 `PascalCase`를 사용하며, 동사로 시작한다.
* **Attributes**: 유니티 인스펙터 노출이 필요한 필드는 `[SerializeField]`를 명시한다.

## 7. Documentation Encoding Standard

* **UTF-8 Only Rule**: `docs/` 내 문서 파일(`.md`, `.txt`)은 UTF-8(UTF-8 with BOM 허용)으로 유지한다.
* **PowerShell Write Rule**: PowerShell로 문서를 수정/생성할 때는 `-Encoding utf8` 옵션을 반드시 명시한다.
* **No Legacy Save Rule**: `cp949`, `euc-kr`, `ansi` 등 레거시 인코딩으로 재저장하지 않는다.
* **Mojibake Diff Rule**: 커밋 전 `git diff`에서 `�`, `??`, `ì`, `ì´` 등 깨짐 패턴을 발견하면 커밋을 중단하고 원인을 분리 진단한다.
* **Recovery Rule**: 이미 깨진 문자열은 인코딩 변환만으로 복원하지 않는다. 정상본(이전 커밋/백업) 기준으로 수동 복구한다.
* **Critical Docs Rule**: `Input_FSM_Flow.md`, `System_Blueprint.md`, `Progress_Log.md`는 편집 전 우선 점검 문서로 취급한다.
