# Boss Raid Portfolio

Unity 기반 3D 보스 레이드 액션 프로젝트입니다.  
싱글플레이 전투 루프를 기반으로, Host authority 멀티플레이 동기화까지 확장한 포트폴리오 프로젝트입니다.

## 프로젝트 개요

- 프로젝트명: `Boss Raid Portfolio`
- 장르: 3D 보스 레이드 액션
- 엔진: `Unity 2022.3.62f3`
- 언어: `C#`
- 주요 패키지: `Input System`, `Netcode for GameObjects`, `Relay`, `Lobby`, `Cinemachine`, `URP`, `UGUI`
- 개발 형태: 개인 포트폴리오 (싱글 + 멀티플레이 구조 설계/구현)

## 내가 구현한 핵심 내용

1. 게임 루프/씬 전환 구조
- `Title -> Loading -> Gameplay -> Result` 흐름을 `TitleSceneController`, `SceneLoader`, `LoadingSceneController`, `GameManager`로 분리했습니다.
- 결과 UI, 재시작, 멀티플레이 재시도 흐름을 `GameManager` 기준으로 통합했습니다.
- Scene contract를 명시적으로 관리해 runtime 전환 안정성을 높였습니다.

2. 전투 시스템 (FSM + 패턴 분리)
- 플레이어/보스 모두 StateMachine 기반으로 이동, 공격, 피격, 사망 흐름을 분리했습니다.
- 보스 공격은 `IBossAttackPattern` 전략 패턴으로 분리해 Basic/Lunge/Projectile/AoE를 독립적으로 관리합니다.
- 공격 경고(telegraph)와 실제 판정 ownership을 분리해 시각/판정 타이밍 회귀를 줄였습니다.

3. 멀티플레이 권한/동기화 구조
- 입력은 `IInputProvider -> PlayerInputPacket` 흐름으로 분리해 network-ready data contract를 유지했습니다.
- 플레이어 행동은 Host 승인(authoritative action start) 기준으로 실행되도록 경계를 정리했습니다.
- 보스는 `MultiplayerBossAuthorityBridge`로 authoritative state를 전파하고, 클라이언트는 display-only 재생을 수행합니다.

4. 성능/안정성 최적화
- `Physics.OverlapSphereNonAlloc` 및 pre-allocated buffer를 사용해 GC 스파이크를 줄였습니다.
- 투사체/이펙트는 풀링 중심으로 관리해 런타임 `Instantiate/Destroy` 비용을 완화했습니다.
- 로그/검증 루틴을 문서화하여 회귀 분석 속도를 높였습니다.

5. 유지보수 중심 문서화
- 아키텍처 변경과 구현 히스토리를 `System_Blueprint`, `Progress_Log`, `Technical_Glossary`에 동기화합니다.
- 작업 단위마다 원인/수정/판단 근거를 남겨 추적 가능한 유지보수 흐름을 만들었습니다.

## 기술 포인트

- Generic FSM 기반 상태 분리 (`StateMachine<TState>`)
- Data-oriented 입력 계약 (`PlayerInputPacket`, bit-packed flags)
- Host authority 기반 멀티플레이 동기화
- Strategy Pattern 기반 보스 공격 확장 구조
- NonAlloc + Pooling 기반 런타임 최적화

## 실행 방법

1. Unity Hub에서 `2022.3.62f3` 에디터를 설치합니다.
2. 프로젝트 루트(`BossRaidPortfolio`)를 엽니다.
3. 기본 시작 씬으로 `Assets/Scenes/mutiplayer/TitleScene.unity`를 실행합니다.
4. 단일 전투 검증이 필요하면 `Assets/Scenes/mutiplayer/GamePlayScene_Verify.unity`를 사용합니다.

## 조작키
- 키보드 이동, 마우스 좌클릭 공격, 스페이스바 대쉬

## 폴더 구조 (요약)

```text
Assets/
  Scenes/
    mutiplayer/   # Runtime target scenes (Title/Loading/GamePlay)
    merged/       # Snapshot/merge reference scenes
    Legacy/       # Legacy/test scenes
  Scripts/
    Player/       # Player controller, states, input
    Boss/         # Boss controller, FSM, attack patterns
    Multiplayer/  # Network authority/sync bridge
    Common/       # Shared systems (game flow, combat core)
    UI/           # HUD and UI controllers
```

## 문서 링크

- [System_Blueprint](/d:/Unity-projects/BossRaidPortfolio/docs/technical/System_Blueprint.md)
- [Input_FSM_Flow](/d:/Unity-projects/BossRaidPortfolio/docs/technical/Input_FSM_Flow.md)
- [Coding_Standard](/d:/Unity-projects/BossRaidPortfolio/docs/technical/Coding_Standard.md)
- [Progress_Log Index](/d:/Unity-projects/BossRaidPortfolio/docs/Progress_Log/README.md)

