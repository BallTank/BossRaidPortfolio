# 🧭 멀티플레이 지터 조사/수정 요약

이 문서는 2026-03-30에 진행한 멀티플레이 owner 화면 jitter 조사와 follow-up 수정 흐름을 한 번에 정리한 요약 문서다.

이 문서의 목적은 아래 3가지다.

* 어떤 문제가 있었는지 한 번에 다시 읽을 수 있게 한다.
* 어떤 가설이 맞았고, 어떤 가설이 틀렸는지 남긴다.
* 현재 코드가 어떤 답으로 정리됐는지 빠르게 공유한다.

중요한 구분:

* 현재 구현 상세 기준 문서는 `docs/technical/multiplayer/Multiplayer_Client_Movement.md` 다.
* 지연 보정 구조 제안안은 `docs/technical/multiplayer/Multiplayer_Lazy_Boundary_Correction_Proposal.md` 다.
* 이 문서는 위 두 문서 사이를 잇는 `조사/수정 히스토리 요약` 이다.

---

## 1. 한 줄 결론

현재 결론은 아래와 같다.

`owner free move jitter의 핵심 원인은 correction spam만이 아니라 raw predicted root tick-step이 camera/body 화면에 그대로 보이던 점이었고, 현재 답은 lazy boundary correction + owner render proxy + predicted owner direct orbit follow다.`

---

## 2. 시작 문제

처음 관찰된 문제는 아래와 같았다.

* owner 자유 이동에서 화면이 `툭툭` 끊겨 보였다.
* 체감상 단순 smoothing 값 문제가 아니라 구조 문제처럼 보였다.
* 당시 owner 화면에는 아래 요소가 함께 섞여 있었다.

1. client prediction
2. Host authoritative correction
3. visual child smoothing
4. camera follow

즉, 아래 의심이 먼저 나왔다.

`root correction과 visual-only smoothing, camera follow가 서로 다른 타이밍으로 겹쳐서 jitter가 생기는가`

---

## 3. 조사 시작점

처음에는 현재 구조를 이렇게 정리했다.

* gameplay truth는 `PredictionReconciliation`
* client owner는 local prediction
* Host는 final authority
* owner visual은 별도 presentation layer
* camera는 raw root를 따라감

여기서 첫 구조 가설은 아래였다.

### 3.1. 1차 가설

`현재 jitter는 correction timing과 visual smoothing timing이 같이 걸리면서 생긴다`

그래서 첫 방향은 아래로 잡았다.

* 자유 이동에서는 correction을 늦춘다
* action 경계에서만 다시 맞춘다
* hard fail은 남긴다

이 답이 `lazy correction with event boundaries` 였다.

---

## 4. 수정 순서 요약

### 4.1. 단계 표

| 단계 | 수정 내용 | 왜 했는가 | 결과 |
| --- | --- | --- | --- |
| 1 | `lazy boundary correction` 1차 도입 | free move correction spam 감소 | 방향은 맞았지만 `hardFailShadow` 가 너무 자주 발동 |
| 2 | `hardFailShadow` 제거 | old shadow snapshot compare가 drift를 과장함 | correction spam 감소 |
| 3 | `idleSettle` 추가 | stop 뒤 medium drift가 오래 남음 | stopped mismatch 정리 |
| 4 | `[MultiplayerCameraFollowTrace]` 추가 | 기존 측정값으로는 남은 jitter 원인 분리가 어려움 | raw root tick-step 확인 |
| 5 | `owner render proxy` 도입 | raw root tick-step을 화면에서 직접 보지 않게 함 | free move jitter 크게 감소 |
| 6 | predicted owner `direct orbit follow` 도입 | render proxy 뒤에도 camera orbit lag가 남을 수 있음 | cameraToDesired lag 제거 |

### 4.2. 바뀐 주요 파일

| 파일 | 역할 |
| --- | --- |
| `Assets/Scripts/Multiplayer/Gameplay/MultiplayerPlayerAvatar.cs` | lazy boundary correction, shadow authoritative state, hard fail / boundary / stale / idleSettle |
| `Assets/Scripts/Multiplayer/Gameplay/MultiplayerPlayerPresentationDriver.cs` | owner render proxy, predicted render smoothing, camera follow proxy 반환 |
| `Assets/Scripts/Camera/ThirdPersonCameraController.cs` | camera follow trace, predicted owner direct orbit follow |
| `docs/technical/multiplayer/Multiplayer_Lazy_Boundary_Correction_Proposal.md` | 설계/후속 메모 |
| `docs/technical/multiplayer/Multiplayer_Client_Movement.md` | current runtime 기준 문서 |

---

## 5. 로그로 확인한 핵심 사실

### 5.1. `hardFailShadow` 는 잘못된 trigger였다

초기 1차 구현 뒤 로그에서 아래 현상이 보였다.

* normal free move에서도 `phase=hardFailShadow` 가 자주 반복됨
* `posError=0.8~0.9m` 가 자주 찍힘

이 값은 진짜 큰 drift라기보다 아래 비교 때문이었다.

* `current predicted state`
* vs `latest shadow authoritative state`

즉, 같은 sequence 비교가 아니라 `몇 tick 늦은 old shadow snapshot` 과 비교하고 있었고,
이건 free move lead 자체를 drift처럼 오해하게 만들었다.

그래서 내린 결론:

`hardFailShadow는 lazy correction의 의도와 충돌하므로 제거해야 한다`

### 5.2. 그 다음에는 반대로 too lazy 문제가 나왔다

`hardFailShadow` 제거 뒤에는 correction spam은 줄었지만,
stop 뒤 medium drift가 오래 남는 로그가 나왔다.

그래서 넣은 것이 `idleSettle` 이다.

핵심 규칙:

* moving 중에는 계속 lazy
* predicted / authoritative 둘 다 거의 멈췄는데 medium drift가 남아 있으면 즉시 settle

### 5.3. 기존 측정값만으로는 남은 jitter 원인을 못 봤다

이 시점에서 로그상 correction phase는 이미 안정적이었다.

하지만 체감 jitter는 남아 있었다.

문제는 기존 trace가 아래를 직접 보여주지 못했다는 점이다.

* `MultiplayerClientMoveTrace`
  * same-sequence correctness는 보여줌
  * 화면 root의 render-frame smoothness는 직접 못 보여줌
* `MultiplayerPredictedRenderTrace`
  * body-vs-root mismatch는 보여줌
  * raw root tick-step은 직접 못 보여줌

그래서 새로 추가한 것이 `[MultiplayerCameraFollowTrace]` 다.

### 5.4. 새 trace가 진짜 원인을 보여줬다

새 camera trace에서 반복적으로 아래 패턴이 확인됐다.

* `anchorStillFrames=7~8`
* 다음에 `anchorPlanarDelta=0.100`

이 값은 아래와 거의 같다.

* move speed `6.0`
* tick rate `60`
* `6.0 / 60 = 0.1m per tick`

즉, 남은 jitter의 핵심은 아래였다.

`correction spam보다 raw predicted root tick-step을 camera/body 화면에서 그대로 보고 있었다`

---

## 6. 그 뒤 실제로 채택한 답

### 6.1. correction policy 답

current correction policy는 아래다.

* 자유 이동에서는 `shadow authoritative state` 로만 저장
* 즉시 sync는 아래에서만 수행
  * `fallback`
  * `boundary sync`
  * `hard fail`
  * `stale sync`
  * `idleSettle`

즉:

`free move는 느슨하게, 중요한 시점만 다시 맞춘다`

### 6.2. 화면 표시 답

current owner 화면 답은 아래다.

* gameplay truth는 raw root
* 화면 표시는 `owner render proxy`
* body와 camera는 같은 proxy를 본다

즉:

`body만 smoothing하고 camera는 raw root를 보는 구조는 다시 쓰지 않는다`

### 6.3. camera orbit 답

render proxy 뒤에도 predicted owner camera가 또 한 번 늦게 따라가면
`cameraToDesired lag` 가 남을 수 있다.

그래서 current follow-up은 아래로 정리했다.

* predicted owner camera는 `direct orbit follow`
* current hidden tuning 기본값
  * `posSmooth=0`
  * `rotSmooth=0`

즉:

`movement smoothing은 render proxy가 맡고, owner local look 응답은 camera가 직접 따라간다`

---

## 7. 최종 로그 상태 요약

마지막 free move 검증 로그 기준으로, 현재 상태는 아래처럼 읽힌다.

### 7.1. correction 쪽

* `hardFail = 0`
* `defer = 0`
* `boundarySync = 0`
* `idleSettle = 0`
* `staleSync = 0`

즉, free move 기준 correction spam은 사실상 사라졌다.

### 7.2. movement step 쪽

* `anchorDelta>=0.099 = 0`

즉, 예전의 `0.1m per tick` 계단 패턴은 현재 free move 로그에서 보이지 않았다.

### 7.3. camera orbit 쪽

* `posSmooth=0`
* `rotSmooth=0`
* `cameraToDesired=0`

즉, camera는 current owner path에서 desired orbit을 바로 보고 있었다.

### 7.4. render proxy 쪽

`MultiplayerPredictedRenderTrace` 에는 여전히 작은 `visualTargetOffset` 이 남을 수 있다.

하지만 current 해석은 아래다.

* 이 값은 `raw root와 render proxy의 차이`
* 즉, 현재는 의도된 smoothing lag
* 예전처럼 `문제의 증거` 가 아니라 `현재 방식이 실제로 동작하고 있다는 흔적`

---

## 8. 현재 판단

현재 판단은 아래와 같다.

### 8.1. free move

`free move 화면 jitter fix는 성공 쪽이다`

### 8.2. 아직 남은 검증

아직 별도로 꼭 봐야 하는 것은 아래다.

* 이동 중 `Attack`
* 이동 중 `Dash`
* 급정지
* 급방향전환

즉, free move는 상당히 정리됐지만,
`boundary sync가 실제 action 경계에서 필요한 만큼만 동작하는가`
는 아직 다시 보는 것이 좋다.

---

## 9. 현재 코드 답 요약

한 문장으로 줄이면:

`Host가 최종 권한을 유지하고, owner 자유 이동에서는 lazy boundary correction으로 correction spam을 줄이며, 화면은 owner render proxy와 predicted owner direct orbit follow로 raw tick-step jitter를 감춘다.`

조금 더 풀면:

1. Host는 계속 final truth다.
2. client owner는 free move에서 local prediction을 유지한다.
3. authoritative snapshot은 먼저 shadow state로 저장한다.
4. correction은 경계 / hard fail / stale / idleSettle에서만 한다.
5. 화면은 raw root 대신 render proxy를 본다.
6. camera는 그 proxy를 다시 늦게 쫓지 않고 바로 따른다.

---

## 10. 관련 문서

* current runtime 기준
  * `docs/technical/multiplayer/Multiplayer_Client_Movement.md`
* 설계 / 후속 메모
  * `docs/technical/multiplayer/Multiplayer_Lazy_Boundary_Correction_Proposal.md`
* 당일 작업 로그
  * `docs/Progress_Log/2026-03-30.md`
* current 구조 청사진
  * `docs/technical/System_Blueprint.md`
* 용어 정리
  * `docs/technical/Technical_Glossary.md`

