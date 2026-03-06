# [Portfolio] Attack4 AoE 수정 회고: 보이는 경계와 실제 데미지 경계를 일치시키기

## 1. 문제 요약
플레이어가 빨간 원 안에 있다고 느끼는데도, 가장자리에서 데미지가 안 들어오는 현상이 있었다.

핵심 원인은 단순했다.

- 시각 경계(플레이어가 보는 원)
- 물리 경계(실제 데미지 판정 원)

이 두 경계가 같은 기준이 아니었다.

---

## 2. Before Logic

```text
1) damage_hit = distance(player, center) <= radius
2) visual_edge = radius * 2    (fallback visual scale path)
```

그래서 아래 구간이 생겼다.

```text
radius < distance <= radius*2
```

이 구간에서 플레이어 체감은:

- 눈에는 빨간 원 안
- 실제 판정은 원 밖
- 결과: 데미지 미적용

즉, "범위 안인데 왜 안 맞음?"의 답은
**플레이어가 본 범위(시각 범위)와 실제 데미지 범위(물리 범위)가 달랐기 때문**이다.

---

## 3. After Logic

```text
1) damage_hit = distance(player, center) <= radius
2) visual_edge = radius * 1.2  (fallback UX bias)
```

이후에는 아래가 성립한다.

- physics-inside => visual-inside
- 시각 경계를 물리보다 약간 크게 보여 "원 밖에서 맞는 느낌"을 줄인다.

정리하면 현재 정책은 "완전 일치"가 아니라 "보수적 경고 UX"다.

- same center
- same damage radius basis
- slightly larger visual boundary for fairness perception

---

## 4. 타이밍 정리 (Single Source)

추가로 Attack4 타이밍도 단일화했다.

- `warningDuration` 하나로
- circle warning 종료(fully red 시작)와
- fire projectile 착지 타이밍(impact marker)을
- 동시에 제어한다.

이제 `impactSyncTime` 분기가 없어서 설정 드리프트가 발생하지 않는다.

---

## 5. 6-Line Trace Card (최신 코드)

[S1] Trigger | AoE cast timer allows spawn | cast interval is ready | `SpawnAoEInstance()` 호출  
[S2] Entry | Circle receives warning data | circle is initialized with radius, warning, active, damage | `StartWarning(... _warningDuration, activeDuration, damage, ..., Attack4Projectile)`  
[S3] Gate | Warning -> full red -> active | warning time is finished | `_phaseTimer >= _warningDuration`이면 `EnterActivePhase()`  
[S4] Core Check | Radius physics check | check colliders inside radius | `Physics.OverlapSphereNonAlloc(transform.position, _radius, ...)`  
[S5] Effect | One circle one hit + invul non-consume | if result is not `Ignored`, store hit target | hit registry 저장  
[S6] Result | Attack4 uses projectile combo route, damage first | valid hit applies damage before combo/stun branch | `HandleProjectileHit` -> `ApplyNormalDamageAndHitReaction` 선적용

---

## 6. 결론
이번 이슈는 "데미지 계산식이 틀렸다"가 아니라
"시각 기준과 물리 기준이 서로 달랐다"가 본질이었다.

기준을 맞추고, 타이밍까지 단일 소스로 묶으니
플레이어 체감과 실제 판정이 같은 규칙으로 동작하게 됐다.
