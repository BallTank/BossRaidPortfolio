# [Portfolio] Boss Raid Portfolio: AoE 안정화와 진행 방향 예측 고도화

## 1. 개요 (Pattern 4 개선 목표)

이번 작업의 핵심은 Pattern 4(AoE)를 "보여주기용 연출"이 아니라, 실제 전투에서 신뢰 가능한 공격 패턴으로 만드는 것이었다.

- 장판 높이 오차(떠 보임/파묻힘) 제거
- 타겟 진행 방향을 반영한 낙하지점 예측
- 코드 수정 없이 인스펙터에서 난이도 튜닝 가능하도록 파라미터 노출

---

## 2. 문제 정의: 맞긴 맞는데, 납득이 안 되는 AoE

기존 AoE는 기능적으로는 동작했지만 체감 문제가 있었다.

- 장판이 지면에서 미세하게 뜨거나 파묻혀 시각 신뢰도가 낮음
- 타겟의 이동 방향을 충분히 반영하지 못해 "피했는데 맞는 느낌"이 생김
- 예측 로직 상수값이 고정되어 튜닝 비용이 큼

---

## 3. 해결 전략: Grounding + Predictive Spread

### 3.1. 장판 Grounding 안정화

`AoECircleController`의 바닥 밀착 보정값을 `fallbackYOffset=0`으로 고정해, 지면 기준 표시 오차를 줄였다.

### 3.2. 낙하지점 예측 확장

`ResolveImpactPoint()`를 타겟의 진행 방향(heading) 기반 예측 확산으로 확장했다.  
아래 파라미터를 인스펙터에서 튜닝 가능하도록 구성했다.

- `headingLeadTime`
- `maxHeadingLeadDistance`
- `forwardSpreadRadius`
- `sideSpreadRadius`
- `headingBias`
- `headingMinSpeed`

핵심은 "현재 위치"만 보는 고정 분포가 아니라, "이동 벡터와 속도 조건"을 반영한 분포로 바꾼 점이다.

---

## 4. Technical Insight

| 기술 포인트 | 적용 내용 | 기대 효과 |
| --- | --- | --- |
| **Grounding Determinism** | `fallbackYOffset=0`으로 지면 밀착 기준 단순화 | 시각 오차/디버깅 혼선 감소 |
| **Predictive Targeting** | heading 기반 리드 + 전방/측면 확산 분리 | 회피/적중 체감의 납득도 향상 |
| **Editor-Tunable Design** | 예측 파라미터 인스펙터 노출 | 코드 수정 없이 난이도/명중률 조절 |
| **Noise Control** | AoE 단계별 임시 디버그 로그 정리 | 콘솔 신호대잡음비 개선 |

---

## 5. 검증

- `dotnet build BossRaidPortfolio.sln` 빌드 성공 (에러 0)
- 장판 표시가 지면 기준으로 일관되게 출력되는지 수동 플레이 테스트로 확인

---

## 6. 결론

이번 개선으로 AoE는 단순 범위 공격에서 "회피 가능하지만 방심하면 맞는" 패턴으로 품질이 올라갔다.  
다음 단계는 플레이어 이동 속도/회피 성향에 따라 예측 파라미터 프리셋을 나누어 페이즈별 체감 난이도를 더 세밀하게 제어하는 것이다.

