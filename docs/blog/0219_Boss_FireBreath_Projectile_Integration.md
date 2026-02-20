# [Portfolio] Boss Raid Portfolio: 보스 불 뿜기 패턴 구현 (Projectile + Flame Attack 연동)

## 1. 개요 (Fire Breath Pattern 목표)

이번 작업의 목표는 보스의 패턴 3(투사체)을 단순 발사 로직이 아니라, 실제 전투 체감이 나는 "불 뿜기" 패턴으로 완성하는 것이었다.

- `telegraph -> 연속 발사 -> 종료` 흐름을 패턴으로 고정
- 보스 애니메이션(`Flame Attack`)과 발사 타이밍 연동
- 유도/높이 추적으로 명중 안정성 확보
- 풀링 기반으로 전투 중 성능 저하 방지

---

## 2. 공격 패턴 설계: Strategy 기반 확장 구조

기존 구조는 `IBossAttackPattern`을 기준으로 `BossAttackState`가 패턴을 실행하는 형태다.  
이번에는 여기에 `ProjectileAttackPattern`을 추가해 패턴 확장성을 유지했다.

- `BossAttackState`: 패턴 실행 책임
- `ProjectileAttackPattern`: 투사체 패턴 전용 로직
- 패턴 시퀀스: `telegraph 0.3s -> 3연발(-8, 0, +8) -> 종료`

핵심은 패턴을 추가해도 상태 실행기(`BossAttackState`)를 크게 수정하지 않는 구조를 지킨 점이다.

---

## 3. Projectile Tracking/Hit 안정화: 누락 없는 피격 처리

실전 테스트에서 가장 먼저 보강한 영역은 "피격 누락 방지"였다.

- `OnTriggerEnter` + `OnCollisionEnter` 동시 지원
- `GetComponent` 실패 시 `GetComponentInParent` 폴백
- Owner `InstanceID` 비교로 자기 자신 피격 차단

유도는 축 분리 전략으로 안정화했다.

- XZ: `RotateTowards` 기반 조향
- Y: `MoveTowards` 기반 점진 추적(`verticalFollowSpeed`)
- 발사 시작 Y는 SpawnPoint 기준, `verticalFollowSpeed = 0`이면 높이 유지

이 방식으로 "연출(입에서 불을 뿜는 느낌)"과 "판정 안정성"을 동시에 맞췄다.

---

## 4. Combat 성능 전략: Object Pooling으로 Zero-GC 지향

투사체는 `Instantiate/Destroy` 대신 `BossProjectilePool`로 재사용한다.

- `prewarmCount = 12`
- `maxCount = 24`
- `expand = true`

풀이 고갈되고 확장도 불가능하면 해당 샷을 스킵하고 경고 로그를 남긴다.  
즉, 전투 프레임을 멈추지 않고 시스템 상태를 가시화하는 방향으로 설계했다.

---

## 5. Technical Insight: 면접관을 위한 핵심 포인트

| 기술 포인트 | 적용 내용 | 기대 효과 |
| --- | --- | --- |
| **Strategy Pattern** | `IBossAttackPattern` 기반 `ProjectileAttackPattern` 추가 | 패턴 확장 시 기존 상태 코드 수정 최소화 |
| **Dual Collision Path** | Trigger/Collision 동시 처리 | 프리팹 물리 세팅 변화에도 피격 안정성 유지 |
| **Parent Damage Lookup** | `GetComponentInParent` 폴백 | 자식 콜라이더 히트 누락 감소 |
| **Axis-Separated Guidance** | XZ 조향 + Y 보간 추적 | 명중률/연출 제어 동시 확보 |
| **Object Pooling** | Prewarm/Max/Expand 기반 재사용 | GC 스파이크 및 런타임 할당 감소 |

---

## 6. 결론 및 향후 계획

이번 작업으로 패턴 3은 "투사체 기능"을 넘어, 보스의 전투 연출과 피격 안정성을 동시에 갖춘 불 뿜기 패턴으로 정리됐다.