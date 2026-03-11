# [Portfolio] Attack3 projectile 넉백 거리 오류 수정

## 1. 문제 요약

처음 보인 증상은 단순했다.

- Attack3 projectile에 맞으면 플레이어가 엉뚱한 곳으로 밀렸다.
- 플레이어의 움직임 로직 문제라고 처음에 생각했다.

처음엔 넉백 거리 버그처럼 보였다.  
하지만 실제로는 distance, direction, collision, input timing, projectile path, hierarchy가 같이 얽혀 있었다.

---

## 2. 시도 목록과 결과

| # | 시도 | 관측 결과 | 결론 |
| --- | --- | --- | --- |
| 1 | hit/stun telemetry 로그 추가 | second-hit 시점, planned move, actual delta, collision을 분리해서 볼 수 있게 됨 | 감으로 보던 문제를 frame data로 바꾸는 단계 |
| 2 | stun 마지막 frame clamp 적용 + impact position 기준 방향 실험 | overshoot는 줄었지만 wrong direction은 남음 | 거리 보정 alone으로는 해결 불가 |
| 3 | push direction을 projectile 기준에서 boss 기준으로 변경 | knockback distance는 안정화됐지만 일부 케이스는 여전히 어색함 | 기준점 변경은 partial fix |
| 4 | `BossHitTrace` 3초 추적 + pre-hit ring buffer 추가 | 어떤 케이스는 hit 전부터 player position이 이미 좋지 않았음 | hit event만 봐서는 원인 분리 불가 |
| 5 | move input release gate 추가 | stun 직후 held input이 바로 재적용되는 흐름 확인 | reaction 이후 movement resume도 체감에 영향 |
| 6 | projectile force source 비교 로그 추가 | `StunState`가 아니라 projectile 쪽 direction source가 더 의심됨 | 남은 문제는 movement보다 force source |
| 7 | projectile transform trace + near-miss trace 추가 | 이상 궤도 자체를 따로 추적 가능해짐 | "player가 밀린다"와 "projectile이 이상하게 돈다"를 분리 |
| 8 | first-hit visual-only flinch 실험 | 특수 규칙은 생겼지만 플레이 규칙이 더 복잡해짐 | 예외 처리보다 단일 규칙이 낫다고 판단 |
| 9 | visual-only 롤백 후 fixed hard-stop reaction으로 통일 | "맞는 동안은 멈춘다"는 규칙이 더 명확해짐 | gameplay rule은 단순해졌음 |
| 10 | scene hierarchy 점검 | 최종 원인은 `BossProjectilePool`이 boss 자식이라 함께 회전한 구조였음 | 코드보다 hierarchy가 핵심 원인 |

---

## 3. 무엇이 아니었는가

이번 로그에서 먼저 지워진 가설은 분명했다.

- `plannedMove`와 `actualDelta`는 거의 같았다.
- `collision=Below` 반복만 보였고, side collision 기반 extra shove는 핵심이 아니었다.
- knockback total distance를 `3.600`으로 고정해도 wrong place 문제는 남았다.
- 즉, 이 이슈는 "얼마나 멀리 밀렸는가"만의 문제가 아니었다.

정리하면, **플레이어 움직임 처럼 보였지만 그게 아니었다.**

---

## 4. 최종 원인

최종 원인은 `BossProjectilePool`의 부모 관계였다.

`BossProjectilePool`이 boss transform의 자식으로 붙어 있었고, 그 결과 projectile이 월드 기준으로 독립적으로 움직이지 못했다.  
그래서 homing code를 계속 의심했지만, 실제로는 projectile의 기준 공간 자체가 boss 회전 영향을 받고 있었다.

문제는 "넉백 공식 한 줄"이 아니라  
**잘못된 hierarchy가 만든 잘못된 이동** 때문이었다.

---

## 5. 결론

1. 먼저 로그를 깔고
2. distance / direction / input / collision 가설을 분리하고
3. 맞지 않는 실험은 롤백하고
4. 마지막에 코드 밖의 원인, 즉 hierarchy를 잡았다

다음부터 무엇이 원인인지 자세히 살펴봐야겠다.
