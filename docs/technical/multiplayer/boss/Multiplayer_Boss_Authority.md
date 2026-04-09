# 🐉 멀티플레이 보스 권한 문서

이 문서는 listen-server 기준에서 boss authority rule을 정리한 기준 문서다.

이 프로젝트의 multiplayer 기준선은 `Host Authority`이며,
boss는 player처럼 owner input을 가지는 오브젝트가 아니라,
Host가 직접 시뮬레이션하는 `server-owned NPC`로 본다.

관련 세부 규칙:

* current aggro source of truth는 `docs/technical/multiplayer/boss/Mutiplayer_Boss_Aggro.md`를 따른다.

---

## 1. 목적 (Purpose)

이 문서의 목적은 아래 4가지를 고정하는 것이다.

* boss가 누구의 authority를 따르는지 정한다.
* boss movement / attack / taking damage / phase change / death의 판정 주체를 정한다.
* Host와 Client가 boss에 대해 각각 무엇을 담당하는지 분리한다.
* boss multiplayer 구현 전에 `minimal replicated state`를 정리한다.

## 2. Sector Split

| 섹터 | 의미 | 기본 역할 |
| --- | --- | --- |
| Boss | Host-owned gameplay actor with replicated state | gameplay state container |
| Host | authority writer | decision / simulation / validation |
| Client | replica reader | presentation / smoothing / local-only feedback |

### 2.1. Boss

boss는 `Host-owned gameplay actor + replicated state`로 정리할 수 있다.

이 말의 뜻은 아래와 같다.

* Host와 Client 모두 boss 오브젝트를 로컬에 하나씩 가진다.
* 하지만 최종 truth는 Host boss가 쓴다.
* client boss는 replicated state를 읽고 화면에 재생한다.
* 이 프로젝트에서는 NGO-based replication path를 사용할 수 있다.

메모:

* current implementation may use a temporary presentation-only mirror on client.
* This does not change the authority contract. Host remains the gameplay truth.

### 2.2. Host

Host는 boss 관련 gameplay truth를 결정한다.

* movement path / rotate / stop
* attack pattern select
* hit window open / close
* projectile spawn and lifetime truth
* damage valid check
* HP change
* phase change
* death

### 2.3. Client

Client는 boss 관련 presentation을 담당한다.

* movement interpolation
* animator state playback
* telegraph, VFX, SFX
* boss HP bar / phase UI
* hit flash / camera shake / local audio

하지만 client는 아래를 final로 결정하지 않는다.

* which attack boss uses
* whether hit was valid
* how much damage boss takes
* when phase changes
* when death starts

---

## 3. Authority Rule By Feature

| 기능 | Host | Client |
| --- | --- | --- |
| movement | authoritative movement sim | interpolation / optional limited extrapolation |
| attack start | pattern choose / start tick / hit timing | telegraph + animation playback |
| projectile / hitbox | spawn / move / activate / resolve | show result |
| taking damage | validate hit, subtract HP, apply hit reaction | show hit flash / UI update |
| phase change | authoritative threshold detect + pending flag + current attack end switch | show Phase UI / VFX |
| death | authoritative start and result | show death animation / UI |

---

## 4. Minimal Replicated State

boss authority 문서는 exact packet shape를 고정하는 문서가 아니다.
하지만 아래 state category는 long-term contract로 유지하는 방향이 안전하다.

| 항목 | 용도 |
| --- | --- |
| transform | boss 위치/회전 표시 |
| locomotion state | idle / chase / turn / special move 표시 |
| current attack id | 어떤 패턴이 재생 중인지 표시 |
| attack start tick or time | telegraph / hit timing / recover timing 기준 |
| attack visual state | same attack 안의 세부 animation phase 표시 |
| attack normalized time / playback speed | host animator progress / speed override 동기화 |
| HP | HUD와 phase 판단 결과 표시 |
| phase | phase-specific visual / logic branch 표시 |
| dead flag | death / result flow |

메모:

* client는 이 state를 바탕으로 visual을 만든다.
* Host는 이 state를 쓰는 쪽이다.
* 가능하면 `minimal data transfer`를 유지한다.
* exact field shape can change, but the authority rule should stay the same.
* 2026-04-09 follow-up 기준 AoE airborne replay bug fix 때문에 `attack visual state`가 `TakeOff / FlyForward / FlyIdle / Land` semantic phase를 직접 담을 수 있다.

---

## 5. Flow

### 5.1. Boss Movement

```mermaid
sequenceDiagram
    participant Host as Host Authority
    participant Boss as Boss Runtime
    participant Client as Client Replica

    Host->>Boss: Run AI and movement
    Host-->>Client: Send authoritative state
    Client->>Client: Interpolate visible movement
```

즉, 보스의 실제 이동 계산은 Host가 수행하고,
Client는 전달받은 상태를 부드럽게 보여 주는 역할에 집중한다.

### 5.2. Boss Attack -> Player Hit

```mermaid
sequenceDiagram
    participant Host as Host Authority
    participant Boss as Boss Runtime
    participant Player as Victim Player
    participant Client as All Clients

    Host->>Boss: Choose attack and open hit window
    Boss->>Host: Report overlap / hit candidate
    Host->>Host: Validate hit and damage
    Host->>Player: Apply authoritative reaction
    Host-->>Client: Replicate attack result
```

### 5.3. Player Attack -> Boss Damage

```mermaid
sequenceDiagram
    participant Owner as Owner Client
    participant Host as Host Authority
    participant Boss as Boss Runtime
    participant Client as All Clients

    Owner->>Host: Send attack intent
    Host->>Host: Validate player attack
    Host->>Boss: Validate boss hit and subtract HP
    Host->>Host: Check phase / death
    Host-->>Client: Replicate HP / phase / reaction
```

이 흐름에서 client는 `보스가 이미 피해를 받았다`를 직접 확정하지 않고,
Host 판정 결과를 반영한다.

---

## 6. Phase Change Rule

현재 대화 기준 기본 규칙은 아래와 같다.

* boss HP가 authoritative 기준으로 `50% 이하`가 되면 Host가 `Phase 2 pending`을 기록한다.
* 실제 `Phase 2 switch`는 현재 attack이 끝난 뒤에 실행한다.

하지만 중요한 점은 `조건이 단순해도 판정 주체는 Host`라는 것이다.

이유:

* HP truth는 Host가 가진다.
* client는 HP snapshot을 약간 늦게 받을 수 있다.
* phase timing은 attack cancel / next pattern / death race와 충돌할 수 있다.

간단한 흐름으로 쓰면:

```mermaid
flowchart LR
    A[Host HP <= 50 detected] --> B[Set Phase2Pending]
    B --> C[Current attack keeps running]
    C --> D[Current attack ends]
    D --> E[Host switches to Phase 2]
    E --> F[Replicate phase result]
```

---

## 7. Client Presentation Note

boss는 player보다 `client prediction` 필요성이 보통 더 낮다.
첫 버전에서는 아래 방향을 권장한다.

* movement는 interpolation 위주
* extrapolation은 필요할 때만 아주 제한적으로
* attack timing은 Host tick 기준
* local-only feedback은 허용
* same-day AoE airborne follow-up 기준 attack id만으로 phase를 알 수 없는 패턴은 `attack visual state`를 같이 읽어 semantic clip transition을 맞춘다

예:

* hit flash
* camera shake
* warning UI
* local sound choice

하지만 이 presentation은 gameplay truth를 바꾸지 않는다.

---
