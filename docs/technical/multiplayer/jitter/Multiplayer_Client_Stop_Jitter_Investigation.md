# 🧪 멀티플레이 클라이언트 Stop Jitter 조사와 수정 요약

이 문서는 2026-04-08에 진행한 `multiplayer client owner` 이동 시작/정지 jitter 조사와 수정 흐름을 한 번에 정리한 기록 문서다.

이 문서의 목적은 아래 3가지다.

* 같은 문제를 나중에 다시 만나도, 어떤 순서로 좁혀 갔는지 바로 읽을 수 있게 한다.
* 무엇이 실제 원인이 아니었는지 남겨, 다음 조사에서 같은 우회를 반복하지 않게 한다.
* 현재 코드가 왜 `animation-side stop settle` 쪽으로 정리되었는지 공유한다.

중요한 구분:

* 현재 구현 상세 기준 문서는 `docs/technical/multiplayer/player/Multiplayer_Client_Movement.md` 다.
* 이 문서는 그 기준 문서의 보조 기록으로, 이번 specific bug의 `investigation + fix history` 를 요약한다.

---

## 1. Problem

관찰된 문제는 아래와 같았다.

* solo play에서는 거의 괜찮았다.
* multiplayer host 화면도 괜찮았다.
* 하지만 multiplayer client owner 화면에서는 `left / right / forward / backward` 입력의 시작과 끝에서 jitter가 남았다.
* user가 처음 체감한 것은 `left <-> right` reverse turn 쪽이었지만, 조사 결과 핵심은 `turn` 자체보다 `move start / move stop` 에 더 가까웠다.

쉬운 영어로 줄이면:

`movement truth looked fine, but the client owner still looked shaky when movement started or stopped.`

---

## 2. Observed Facts

### 2.1. 6-Line Trace Card

1. `[S1] Trigger | owner client releases move key | Assets/Scripts/Multiplayer/Gameplay/MultiplayerPlayerAvatar.cs:562 | predicted owner tick keeps running`
2. `[S2] Entry | client prediction logs same stop frame | Assets/Scripts/Multiplayer/Gameplay/MultiplayerPlayerAvatar.cs:1654 | input=(0,0), planarVel=0`
3. `[S3] Gate | authoritative correction is skipped | Assets/Scripts/Multiplayer/Gameplay/MultiplayerPlayerAvatar.cs:260 | posDelta=0, yawDelta=0`
4. `[S4] Core Check | render proxy is not snapping | Assets/Scripts/Multiplayer/Gameplay/MultiplayerPlayerPresentationDriver.cs:82 | snap=False`
5. `[S5] Effect | animator Speed stays high while stopped | Assets/Scripts/Player/PlayerController.cs:427 | lingering locomotion blend`
6. `[S6] Result | only client owner still feels jittery on stop | Assets/Scripts/Player/PlayerController.cs:458 | stop settle missing`

### 2.2. What The Logs Proved

런타임 로그를 통해 아래 사실을 확인했다.

* correction deadzone을 크게 바꿔도 bad window에서 `posDelta=0`, `yawDelta=0` 이 계속 찍혔다.
* proxy path는 `snap=False` 이고 `proxyTargetDelta=0` 인데도 체감 jitter가 남았다.
* camera trace도 `cameraToDesired=0` 인 구간이 많아, camera lag 자체가 핵심은 아니었다.
* 마지막까지 남은 값은 owner client의 locomotion `animSpeed` 였다.

쉬운 영어로 줄이면:

`correction was clean, proxy was clean, camera was mostly clean, but the locomotion blend was still hanging.`

---

## 3. Approach History

| 단계 | 시도 | 왜 시도했는가 | 결과 |
| --- | --- | --- | --- |
| 1 | locomotion `Speed` damping 추가 | reverse turn 중 one-frame idle cut-in 완화 | solo / host는 좋아졌지만 client owner는 잔여 jitter 유지 |
| 2 | correction deadzone loosen debug | start/stop tiny correction이 원인인지 확인 | `0.03 / 0.06 / 0.1` 모두 큰 차이 없음 |
| 3 | movement trace 추가 | root / correction / proxy / animator 중 어느 layer인지 분리 | root/proxy/correction은 안정적이고 `animSpeed`만 남는 것 확인 |
| 4 | predicted owner `Speed` single writer | network tick/replay direct write 제거 | client jitter가 많이 줄었지만 완전 해결은 아님 |
| 5 | `CharacterController.velocity` -> predicted planar speed cache | frame writer source를 더 믿을 수 있는 값으로 교체 | 구조는 더 맞아졌지만 real stop lingering은 남음 |
| 6 | stop-aware settle (`0.03s` grace) | brief neutral frame과 real stop을 분리 | 최종 해결 |

---

## 4. What Did Not Fix It

### 4.1. Correction Deadzone Loosen

이 방법은 이번 문제를 해결하지 못했다.

이유:

* bad window에서 이미 correction이 거의 일어나지 않고 있었다.
* 로그상 authoritative compare는 `within correction deadzone` 이 반복되었고,
* `posDelta` 와 `yawDelta` 도 사실상 `0` 이었다.

즉:

`The client was not shaking because Host kept correcting it.`

### 4.2. Proxy Smooth / Snap Tuning

이 방법도 이번 문제의 중심은 아니었다.

이유:

* stop window에서 proxy는 이미 `snap=False`
* target delta도 거의 `0`
* visual transform 자체는 안정적이었다

즉:

`The client was not shaking because the render proxy was snapping.`

### 4.3. Source Swap Alone

`CharacterController.velocity` 대신 predicted planar speed cache를 쓰는 것은 맞는 방향이었다.
하지만 이것만으로는 final stop lingering이 완전히 사라지지 않았다.

이유:

* source는 더 정확해졌지만,
* `brief neutral frame` 과 `real stop` 을 같은 damping rule로만 처리하고 있었기 때문이다.

---

## 5. Root Cause

최종 root cause는 아래 한 줄로 정리된다.

`이번 client owner jitter의 마지막 원인은 network correction이나 root movement가 아니라, real stop 구간에서 locomotion Animator Speed가 너무 늦게 settle되던 animation-side lingering 이었다.`

조금 더 풀어서 쓰면:

* opposite turn smoothness를 위해 one-frame neutral input을 너무 빨리 `idle` 로 보내면 안 됐다.
* 하지만 same rule을 real stop에도 그대로 쓰면 walk blend가 너무 오래 남았다.
* 그래서 `brief neutral frame` 과 `real stop` 을 분리해야 했다.

---

## 6. Final Fix

현재 최종 fix는 아래 구조다.

1. predicted owner locomotion `Speed`는 `PlayerController` normal `Update` 가 single writer로만 쓴다.
2. source는 `CharacterController.velocity` 가 아니라 latest predicted planar speed cache를 쓴다.
3. short neutral frame은 기존 locomotion damping을 유지한다.
4. 하지만 input magnitude와 predicted planar speed가 함께 거의 `0` 인 상태가 `0.03s` 넘게 유지되면, locomotion `Speed` 를 immediate `0` 으로 settle한다.

대표 코드 위치:

* `Assets/Scripts/Player/PlayerController.cs:112`
* `Assets/Scripts/Player/PlayerController.cs:427`
* `Assets/Scripts/Player/PlayerController.cs:458`
* `Assets/Scripts/Multiplayer/Gameplay/MultiplayerPlayerAvatar.cs:1395`

쉬운 영어로 줄이면:

`keep reverse-turn smoothing for tiny neutral frames, but treat a real stop as a different case and settle it quickly.`

---

## 7. Why This Worked

이 fix가 먹힌 이유는 current bad layer만 직접 고쳤기 때문이다.

* correction layer를 다시 흔들지 않았다.
* presentation proxy를 다시 흔들지 않았다.
* movement truth / replay / authority 구조도 다시 흔들지 않았다.
* 남은 문제였던 `animation stop settling` 만 좁게 다뤘다.

즉:

`The final fix worked because it matched the last remaining bad signal in the trace.`

---

## 8. Another Way Later

이번 버그를 고친 뒤에도 later polish 후보는 남아 있다.

### 8.1. Pivot / Turn Animation

`150°` 정도의 큰 reverse turn에는 dedicated pivot animation을 넣을 수 있다.

이건 이번 bug의 root fix는 아니었지만,
later polish로는 여전히 좋은 방향이다.

### 8.2. Animator State Separation

나중에 필요하면 locomotion `Speed` 하나만으로 처리하지 않고,

* `keep locomotion alive for tiny neutral turn`
* `real stop settle`

를 더 분리한 animator rule로 확장할 수 있다.

---

## 9. Validation

이번 fix는 아래 케이스를 기준으로 확인했다.

* `Unity Editor = client`
* `Build = host`

주요 테스트:

* `left press -> left release`
* `right press -> right release`
* `left -> right`
* `right -> left`

로그 판독 기준:

* correction은 `AuthSkip`
* owner predicted state는 `ClientPredict`
* render proxy는 `Proxy`

성공 기준:

* stop window에서 `input=(0,0)` / `planarVel=0`
* correction delta는 `0`
* proxy snap은 `False`
* locomotion `animSpeed` 가 lingering 없이 빠르게 내려가야 한다

---

## 10. Related Docs

* `docs/technical/multiplayer/player/Multiplayer_Client_Movement.md`
* `docs/technical/System_Blueprint.md`
* `docs/technical/Technical_Glossary.md`
* `docs/Progress_Log/2026-04-08.md`
