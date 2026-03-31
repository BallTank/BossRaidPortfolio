# [Interview] Host 화면에서 client locomotion 애니메이션이 보이지 않던 문제

## 1. 한 줄 요약

* Host 화면에서는 remote client의 attack/dash는 보였지만 idle/walk는 보이지 않았고, 최종 원인은 Host의 `AuthoritativeLocomotion` 경로가 remote avatar의 locomotion animator handoff와 `Speed` 갱신을 충분히 하지 않던 것이었다.

---

## 2. 문제 상황

### 2.1. 보이는 현상

* 2-player 멀티플레이 기준으로 Host 화면에서 remote client player를 볼 때 문제가 드러났다.
* attack과 dash는 Host에서 정상적으로 재생됐다.
* 하지만 locomotion 계열인 idle/walk는 Host에서 빠지거나 정상적으로 보이지 않았다.

### 2.2. 처음에 헷갈린 이유

* 처음에는 Animator transition 문제처럼 보일 수 있었다.
* 하지만 action animation이 정상적으로 보인다는 점 때문에, 전면적인 Animator asset 문제라고 단정하기는 어려웠다.
* 그래서 “왜 locomotion만 따로 깨지는가”를 설명할 수 있는 구조적 원인을 먼저 찾는 쪽이 맞다고 봤다.

---

## 3. 첫 가설

### 가설 1

* 무엇을 의심했는가:
  * Animator transition 또는 state setup 문제
* 왜 그렇게 생각했는가:
  * idle/walk가 빠지면 보통 `Locomotion` state 진입이나 blend tree 설정을 먼저 의심하기 쉽다.
* 어떻게 확인하려고 했는가:
  * locomotion, attack, dash가 같은 animation 실행 경로를 타는지 비교하려고 했다.

### 가설 2

* 무엇을 의심했는가:
  * `NetworkAnimator`가 locomotion만 제대로 동기화하지 못하는 문제
* 왜 그렇게 생각했는가:
  * 멀티플레이 화면에서 특정 animation만 빠질 때는 network animator sync를 같이 의심할 수 있다.
* 어떻게 확인하려고 했는가:
  * prefab에 `NetworkAnimator`가 붙어 있는지와, 실제 animator 갱신이 어디서 일어나는지 구분해서 보려고 했다.

### 가설 3

* 무엇을 의심했는가:
  * locomotion만 별도 network path를 타고 있고, 그 경로 안에서 host-side animator 갱신이 빠졌을 가능성
* 왜 그렇게 생각했는가:
  * attack/dash는 보이는데 locomotion만 빠진다면, 같은 Animator의 모든 기능이 고장난 것이 아니라 **경로가 갈라져 있을 가능성**이 높다.
* 어떻게 확인하려고 했는가:
  * `MultiplayerPlayerAvatar`, `PlayerController`, `MoveState`, `PlayerLocomotionCore`를 따라가며 move-only path와 action path를 나눠서 읽었다.

---

## 4. 가설을 줄여 간 과정

### 4.1. 처음 본 포인트

* 가장 먼저 확인한 코드/동작:
  * `MoveState`, `AttackState`, `DashState`
* 그 지점을 먼저 본 이유:
  * 문제의 핵심이 “왜 locomotion만 다르게 보이는가”였기 때문에, locomotion과 action이 같은 state path를 타는지 먼저 확인하는 것이 가장 빨랐다.

### 4.2. 버린 가설

* 어떤 가설을 약하게 봤는가:
  * “전체 Animator transition 설정이 잘못됐다”는 가설
* 왜 약해졌는가:
  * `AttackState`와 `DashState`는 각각 직접 `CrossFade`로 animation을 재생하고 있었고, 실제로 Host에서도 그 animation은 보였다.
  * 즉, Animator 전체가 망가진 그림은 아니었다.

### 4.3. 강해진 가설

* 어떤 가설이 점점 유력해졌는가:
  * locomotion-only 구간이 별도 network path를 타고 있고, 그 안에서 visual sync가 빠졌다는 가설
* 무엇이 결정적인 단서였는가:
  * Host는 move-only 입력일 때 `AuthoritativeLocomotion` mode를 사용했고, non-locomotion 입력에서는 `Full` FSM fallback으로 돌아가고 있었다.
  * 이 시점에서 attack/dash와 locomotion이 같은 실행 경로가 아니라는 점이 분명해졌다.

### 4.4. 추적 단계 표

| 단계 | 무엇을 확인했는가 | 왜 확인했는가 | 결과 |
| --- | --- | --- | --- |
| 1 | `AttackState`, `DashState`의 animation 실행 방식 | action이 보이는 이유를 먼저 확인하려고 | 둘 다 direct `CrossFade` 경로였다 |
| 2 | `PlayerController.Update()`의 simulation mode 분기 | locomotion과 action이 같은 update path인지 확인하려고 | `Full` 과 `PredictedLocomotion`/`AuthoritativeLocomotion`이 나뉘어 있었다 |
| 3 | `MultiplayerPlayerAvatar`의 Host authority mode 선택 | Host가 언제 special locomotion path를 쓰는지 확인하려고 | `MoveState && buttons == 0`에서 locomotion-only authority path가 켜졌다 |
| 4 | `SimulateNetworkLocomotionTick(...)` 호출부 | special path가 animator까지 갱신하는지 확인하려고 | Host locomotion tick 쪽에서 animator 갱신과 locomotion handoff가 부족했다 |

---

## 5. 실제 원인

* 최종 원인을 한 문장으로 정리하면:
  * Host는 remote client의 locomotion truth는 계산하고 있었지만, move-only authority path 안에서 remote avatar animator를 `Locomotion`으로 확실히 되돌리고 `Speed`를 계속 맞추는 처리가 비어 있었다.
* 구조적으로 보면:
  * 현재 멀티플레이 구조는 `PredictionReconciliation` 한 경로를 쓰지만, 그 안에서 move-only 구간은 `AuthoritativeLocomotion`, non-locomotion 구간은 `Full` fallback으로 나뉜다.
  * 문제는 이 split path 중에서 **Host locomotion-only visual handoff** 쪽에 있었다.
* 처음 직감과 실제 원인의 차이는:
  * 처음에는 Animator transition 같은 asset/setup 문제도 의심했지만, 실제로는 multiplayer authority path 안의 host-side visual sync 문제였다.

---

## 6. 수정 내용

### 6.1. 무엇을 바꿨는가

* 수정한 파일:
  * `Assets/Scripts/Multiplayer/Gameplay/MultiplayerPlayerAvatar.cs`
* 바꾼 핵심 로직:
  * Host authority locomotion tick에서도 animator update를 켰다.
  * `AuthoritativeLocomotion` mode 재진입 시 `Locomotion` state로 다시 handoff하고 현재 input magnitude로 `Speed`를 바로 맞추도록 했다.

### 6.2. 수정 전 흐름

* before:
  * Host가 remote client의 locomotion truth는 계산한다.
  * 하지만 move-only authority path에서 remote avatar animator는 충분히 복구되지 않는다.
  * 그래서 transform은 움직여도 idle/walk visual이 빠질 수 있다.

### 6.3. 수정 후 흐름

* after:
  * Host가 move-only authority path로 다시 들어올 때 remote avatar animator를 `Locomotion`으로 돌린다.
  * fixed-tick locomotion sim에서도 `Speed`를 같이 갱신한다.
  * 그래서 Host 화면에서도 client idle/walk가 계속 보이게 된다.

### 6.4. 왜 이 수정이 맞았는가

* 더 큰 리팩터링 대신 이 수정으로 충분했던 이유:
  * 문제는 locomotion 수학식 자체보다 host-side visual handoff 누락에 가까웠다.
  * 그래서 전체 prediction/correction 구조를 다시 쓰지 않아도 됐다.
* 회귀 위험을 어떻게 줄였는가:
  * `AuthoritativeLocomotion` path 안에서만 좁게 수정했다.
  * action path나 owner prediction path는 건드리지 않았다.

---

## 7. 검증

### 7.1. 정적 검증

* 빌드/컴파일:
  * `dotnet build Assembly-CSharp.csproj -nologo`
  * 결과: 성공 (`0` errors)
* 로그/코드 diff 확인:
  * Host locomotion tick에서 `updateAnimator=true`
  * `EnterAuthoritativeLocomotionMode()`에서 `Locomotion` + `Speed` handoff 추가

### 7.2. 런타임 검증

* 실제 플레이에서 무엇을 확인했는가:
  * 이 문서 작성 시점 기준으로 코드/문서 정리와 빌드 검증까지 완료했다.
* 어떤 케이스까지 확인했는가:
  * 정적 코드 추적과 컴파일 기준 검증은 끝냈다.

### 7.3. 남은 리스크

* 아직 추가로 확인할 부분:
  * 실제 2-peer 플레이에서 Host 화면의 remote client idle/walk가 안정적으로 보이는지 재확인해야 한다.
  * attack/dash에서 locomotion으로 복귀하는 타이밍이 자연스러운지도 함께 봐야 한다.

---

## 8. 면접 답변 톤으로 다시 말하기

### 8.1. 30초 답변

짧고 또렷한 버전:

* 이 이슈는 전체 Animator 문제라기보다, locomotion만 별도 Host authority path를 타고 있었기 때문에 생긴 split-path bug였습니다. attack/dash는 `Full` FSM fallback에서 직접 animation을 재생해서 보였고, locomotion-only path는 movement truth는 계산하지만 remote animator handoff가 부족했습니다. 그래서 Host의 `AuthoritativeLocomotion` 경로에서 `Locomotion` 복귀와 `Speed` 갱신을 함께 넣어 해결했습니다.

### 8.2. 90초 답변

`문제 정의 -> 가설 -> 추적 -> 원인 -> 수정 -> 검증` 순서 버전:

* 처음에는 Animator transition 문제도 의심했지만, attack과 dash가 Host에서 잘 보인다는 점 때문에 전면적인 Animator asset 문제는 아닐 수 있다고 봤습니다.
* 그래서 locomotion과 action이 같은 실행 경로인지부터 확인했고, action은 `Full` FSM fallback에서 direct `CrossFade`를 타지만 move-only 구간은 `AuthoritativeLocomotion`이라는 별도 Host authority path를 쓴다는 점을 찾았습니다.
* 그 다음 그 path를 따라가면서 Host가 remote client의 movement truth는 계산하지만, remote avatar animator를 `Locomotion`으로 확실히 돌리고 `Speed`를 계속 맞추는 handoff가 비어 있다는 점을 확인했습니다.
* 그래서 locomotion sim 호출에서 animator update를 다시 켜고, `AuthoritativeLocomotion` 재진입 시 `Locomotion` + `Speed`를 같이 맞추도록 수정했습니다.
* 정적 코드 추적과 빌드 검증까지 완료했고, 남은 확인은 실제 2-peer 런타임에서 Host 화면의 client idle/walk가 안정적으로 보이는지 보는 것입니다.

---

## 9. 배운 점

* 이번 문제에서 얻은 디버깅 관점:
  * animation bug처럼 보여도 먼저 “무엇은 되고 무엇은 안 되는가”를 비교하면 split-path 구조를 더 빨리 찾을 수 있다.
* 다음에 비슷한 문제를 만나면 더 빨리 볼 포인트:
  * asset setup보다 먼저 실행 경로가 몇 개로 갈라지는지 본다.
  * solo path, Host authority path, owner prediction path가 같은 animation policy를 쓰는지 비교한다.
* 이번 경험이 구조 이해에 준 도움:
  * `PredictionReconciliation` 안에서도 move-only slice와 `Full` fallback의 역할이 다르다는 점을 더 분명히 이해하게 됐다.

---

## 10. 꼬리 질문 대비

### 질문 1

* 질문:
  * 왜 attack/dash는 보이는데 locomotion만 안 보였나요?
* 답변:
  * attack/dash는 `Full` FSM fallback에서 direct animation 재생을 타고 있었고, locomotion만 move-only authority path를 따랐습니다. 그래서 split path 중 locomotion path의 visual sync 누락이 따로 드러났습니다.

### 질문 2

* 질문:
  * 왜 더 큰 리팩터링 대신 좁은 수정으로 끝냈나요?
* 답변:
  * 문제 범위가 locomotion authority path 안의 host-side visual handoff로 좁혀졌기 때문입니다. prediction/correction 구조 전체를 바꾸는 것보다 해당 경로만 고치는 편이 회귀 위험이 훨씬 낮았습니다.

### 질문 3

* 질문:
  * 이 문제가 transition 문제는 아니라고 어떻게 판단했나요?
* 답변:
  * 같은 Animator를 쓰는 attack/dash가 Host에서 정상적으로 재생되고 있었기 때문입니다. 그래서 전체 transition setup보다 실행 경로 차이를 먼저 의심하는 것이 더 합리적이었습니다.

---

## 11. 피해야 할 표현

* “그냥 animation flag를 켜니까 됐습니다.”
* “정확히는 모르겠는데 이 코드가 문제였습니다.”
* “감으로 바꿨더니 해결됐습니다.”

대신 아래처럼 말한다:

* “처음에는 Animator setup 문제도 의심했지만, 동작 차이를 비교하면서 split path 문제로 좁혔습니다.”
* “문제의 범위가 locomotion authority path에 한정되어 있었기 때문에 그 경로만 좁게 수정했습니다.”
* “수정 후에는 적어도 코드 추적과 빌드 기준으로는 일관성이 맞는 것을 확인했습니다.”
