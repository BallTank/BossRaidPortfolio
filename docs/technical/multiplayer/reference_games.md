# reference_games

## 목적

- `Host authority` 또는 `Server authority` 방향의 공개 레퍼런스 게임/샘플을 정리한다.
- 우리 프로젝트의 `client movement feel + Host truth` 문제를 볼 때, 어떤 레퍼런스가 실제로 도움이 되는지 빠르게 비교한다.
- 이것은 "그대로 복사할 설계" 문서가 아니라, `what to learn / what not to copy` 문서다.

## 빠른 결론

| 이름 | 핵심 모델 | movement 방식 | 우리 프로젝트 적합도 |
| --- | --- | --- | --- |
| Boss Room | server-authoritative | client sends move target, server moves character | 전체 구조 참고용으로 좋음 |
| TheEndGame | fully authoritative + prediction/reconciliation | tick input, server sim, rollback/replay | 현재 movement 문제에 가장 가까움 |
| PredictionReconciliationNetwork | generic prediction/reconciliation sample | processor/input/state abstraction | 구조 참고용으로 좋음 |
| MultiplayerProject | authoritative server + prediction/reconciliation + interpolation | local predict, server correct, replay pending input | 개념 참고용으로 좋음 |

## 1. Boss Room

- Repo: https://github.com/Unity-Technologies/com.unity.multiplayer.samples.coop
- Source note:
  - README says `The game is server-authoritative`
  - movement code is pointed to `ServerCharacterMovement.cs`

### authority

- `host` is also the `server`
- gameplay is `server-authoritative`
- README:
  - `One of the eight clients acts as the host/server`
  - `The game is server-authoritative, with latency-masking animations`

### movement

Easy English flow:

`client click -> send move target RPC -> server pathfinds and moves -> NetworkTransform syncs position/rotation`

Key points:

- input is not "move vector every tick"
- it is `click-to-move`
- client sends world target to server
- server side character movement uses `NavMeshAgent`
- position updates use `NetworkTransform`
- feel is helped by `latency-masking animations`, not local locomotion prediction

Useful references:

- `C:\Users\user\AppData\Local\Temp\BossRoom_inspect\README.md:126`
- `C:\Users\user\AppData\Local\Temp\BossRoom_inspect\README.md:130`
- `C:\Users\user\AppData\Local\Temp\BossRoom_inspect\Assets\Scripts\Gameplay\GameplayObjects\Character\ServerCharacter.cs:193`
- `C:\Users\user\AppData\Local\Temp\BossRoom_inspect\Assets\Scripts\Gameplay\GameplayObjects\Character\ServerCharacterMovement.cs:20`
- `C:\Users\user\AppData\Local\Temp\BossRoom_inspect\Assets\Scripts\Gameplay\GameplayObjects\Character\ServerCharacterMovement.cs:81`
- `C:\Users\user\AppData\Local\Temp\BossRoom_inspect\Assets\Scripts\Gameplay\GameplayObjects\Character\ServerCharacterMovement.cs:173`
- `C:\Users\user\AppData\Local\Temp\BossRoom_inspect\Assets\Scripts\Gameplay\GameplayObjects\Character\ServerCharacterMovement.cs:244`

### other multiplayer things

- action/skill requests go through server-side action players
- movement and action interruption rules are kept on server
- host/client visual gap is hidden with anticipation and animation tricks

### what to learn

- good example for:
  - server-owned gameplay truth
  - scene/session/gameplay architecture
  - separating `input`, `movement`, `action`, `visualization`

### what not to copy directly

- its `click-to-move NavMesh` path is not our control model
- it does not solve our exact `third-person direct locomotion feel` problem
- it is more useful for `overall multiplayer architecture` than for `our player motor`

## 2. TheEndGame

- Repo: https://github.com/DylanHabs/TheEndGame
- Demo page: https://dylanhabs.github.io/server.html
- Source note:
  - demo page describes it as `fully authoritative and deterministic 3D platformer`

### authority

- fully authoritative server model
- clients send inputs
- server simulates final truth
- client predicts locally and corrects when server state arrives

### movement

Easy English flow:

`client captures input with tick -> client simulates now -> server buffers same input -> server simulates -> server sends authoritative motor state -> client rewinds if wrong -> client replays pending inputs`

Key points:

- custom tick config exists
- input packets include move bits, camera yaw, and tick id
- server keeps per-player sorted input buffer
- missing packets are handled with buffered last input fallback
- server sends back full movement-related state:
  - position
  - rotation
  - velocity
  - grounding flags
- client compares server state with stored prediction
- if error is bigger than threshold, client applies server state and replays saved inputs
- movement core uses `KinematicCharacterController`, not Unity `CharacterController`

Useful references:

- `C:\Users\user\AppData\Local\Temp\TheEndGame_inspect\Assets\Scripts\Multiplayer\GamePackets.cs:36`
- `C:\Users\user\AppData\Local\Temp\TheEndGame_inspect\Assets\Scripts\Multiplayer\MovementManagerClient.cs:38`
- `C:\Users\user\AppData\Local\Temp\TheEndGame_inspect\Assets\Scripts\Multiplayer\MovementManagerClient.cs:49`
- `C:\Users\user\AppData\Local\Temp\TheEndGame_inspect\Assets\Scripts\Multiplayer\MovementManagerClient.cs:65`
- `C:\Users\user\AppData\Local\Temp\TheEndGame_inspect\Assets\Scripts\Multiplayer\MovementManagerClient.cs:153`
- `C:\Users\user\AppData\Local\Temp\TheEndGame_inspect\Assets\Scripts\Multiplayer\MovementManagerServer.cs:20`
- `C:\Users\user\AppData\Local\Temp\TheEndGame_inspect\Assets\Scripts\Multiplayer\MovementManagerServer.cs:42`
- `C:\Users\user\AppData\Local\Temp\TheEndGame_inspect\Assets\Scripts\Multiplayer\MovementManagerServer.cs:71`
- `C:\Users\user\AppData\Local\Temp\TheEndGame_inspect\Assets\Scripts\Multiplayer\MovementManagerServer.cs:93`
- `C:\Users\user\AppData\Local\Temp\TheEndGame_inspect\Assets\Scripts\Player.cs:53`
- `C:\Users\user\AppData\Local\Temp\TheEndGame_inspect\Assets\Scripts\Player.cs:88`

### other multiplayer things

- custom connection managers on client/server
- packet id based transport
- intentional lag simulation settings exist
- server sends full player state snapshots, not just simple transform sync

### what to learn

- best movement reference for our current work
- especially useful for:
  - ticked input buffer
  - replay rules
  - authoritative state payload
  - separating owner prediction from remote state apply

### caution

- this is still its own custom networking stack
- it is not Netcode for GameObjects
- we should learn the architecture, not copy file-by-file

## 3. PredictionReconciliationNetwork

- Repo: https://github.com/TCleard/PredictionReconciliationNetwork
- Source note:
  - README says it is for `Client-side prediction and Server reconciliation`

### authority

- generic role model:
  - `SERVER`
  - `OWNER`
  - `HOST`
  - `GUEST`
- owner predicts
- server generates authoritative states
- owner reconciles if inconsistent

### movement

Easy English flow:

`input provider -> processor generates predicted state -> input goes to server -> server processes same input -> server sends state -> consistency checker decides mismatch -> rewind -> replay newer inputs`

Key points:

- it formalizes the system into:
  - `Input`
  - `State`
  - `Processor`
  - `StateConsistencyChecker`
  - `NetworkHandler`
  - `Ticker`
- the sample processor uses `CharacterController.Move()`
- rewind is explicit in `Processor.Rewind(...)`
- replay is automatic after inconsistency

Useful references:

- `git show HEAD:README.md` from local inspection clone
- repo: https://github.com/TCleard/PredictionReconciliationNetwork

### other multiplayer things

- anti-hack ticker detection
- optional sync policy
- can be adapted to NGO
- this is more framework/sample than full game

### what to learn

- very good for:
  - naming the pieces clearly
  - understanding clean prediction/reconciliation roles
  - small prototype architecture

### caution

- it is not a full game reference
- sample movement still uses `CharacterController.Move()`
- that part is important for us because our recent jitter problem also came from replay-core behavior

## 4. MultiplayerProject

- Repo: https://github.com/alexander-scott/MultiplayerProject
- Source note:
  - README says `Authoritative Server`
  - README also says it uses `Client-Side Prediction`, `Server Reconciliation`, and `Entity Interpolation`

### authority

- dedicated authoritative server
- clients only send input
- server simulates and broadcasts authoritative state

### movement

Easy English flow:

`local client reads input -> sequence number added -> local player predicts immediately -> packet goes to server -> server applies same input -> server sends authoritative player state back -> client restores authoritative state -> client reapplies pending inputs`

Key points:

- local prediction is optional by flag
- queued update packets store input + sequence
- server remembers last processed sequence number
- returned server state includes processed sequence id
- client discards old confirmed inputs and reapplies the rest
- remote players use interpolation

Useful references:

- `C:\Users\user\AppData\Local\Temp\MultiplayerProject_inspect\README.md:11`
- `C:\Users\user\AppData\Local\Temp\MultiplayerProject_inspect\README.md:18`
- `C:\Users\user\AppData\Local\Temp\MultiplayerProject_inspect\README.md:22`
- `C:\Users\user\AppData\Local\Temp\MultiplayerProject_inspect\README.md:24`
- `C:\Users\user\AppData\Local\Temp\MultiplayerProject_inspect\MultiplayerProject\Source\Scenes\Client\Game\GameScene.cs:132`
- `C:\Users\user\AppData\Local\Temp\MultiplayerProject_inspect\MultiplayerProject\Source\Scenes\Client\Game\GameScene.cs:189`
- `C:\Users\user\AppData\Local\Temp\MultiplayerProject_inspect\MultiplayerProject\Source\Scenes\Client\Game\GameScene.cs:237`
- `C:\Users\user\AppData\Local\Temp\MultiplayerProject_inspect\MultiplayerProject\Source\Networking\Server\GameInstance.cs:142`
- `C:\Users\user\AppData\Local\Temp\MultiplayerProject_inspect\MultiplayerProject\Source\Networking\Server\GameInstance.cs:236`

### other multiplayer things

- waiting room / lobby / game room flow
- multiple game instances
- protobuf serialization
- remote entity interpolation

### what to learn

- good concept reference for:
  - prediction + reconciliation loop
  - remote interpolation
  - authoritative lobby/game-room structure

### caution

- 2D MonoGame project
- not Unity
- not a direct motor implementation reference for our 3D controller

## 우리 프로젝트와 비교

| 항목 | Boss Room | TheEndGame | PredictionReconciliationNetwork | MultiplayerProject | 우리 프로젝트 |
| --- | --- | --- | --- | --- | --- |
| authority | server-authoritative | fully authoritative | server-authoritative structure | authoritative server | Host authority 유지 |
| movement input | click target | move input per tick | input object per tick | keyboard input + sequence | move/look/buttons per tick |
| movement core | NavMeshAgent | KinematicCharacterController | sample uses CharacterController | custom 2D player logic | 현재는 custom rollback/replay + locomotion core 실험 중 |
| client immediate feel | anticipation only | prediction | prediction | prediction | prediction/replay 필요 |
| correction 방식 | mostly transform sync + visuals | rewind + replay | rewind + replay | restore + replay | rewind + replay 방향 |
| remote rendering | NetworkTransform | authoritative state apply | guest state apply | interpolation | authoritative snapshot / sync |
| 우리에게 가장 useful 한 것 | overall architecture | movement architecture | clean abstraction | concept + queue/replay | - |

## 실무 메모

- `Boss Room`
  - best for `overall Unity multiplayer architecture`
  - not best for our direct locomotion feel issue

- `TheEndGame`
  - best for `movement authority problem`
  - especially useful for `tick input`, `buffer`, `state payload`, `rewind/replay`

- `PredictionReconciliationNetwork`
  - best for `clean mental model`
  - good when we want to simplify class roles

- `MultiplayerProject`
  - best for `concept validation`
  - helpful to compare how sequence/replay/interpolation are explained in simple form

## 현재 결론

- `GalacticKittens` was useful as a contrast sample, but not a good direct fit for our `Host-authority direct locomotion`.
- If we want one movement reference first, inspect `TheEndGame`.
- If we want one full Unity architecture reference first, inspect `Boss Room`.
