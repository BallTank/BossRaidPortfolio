# Progress Log Index

Boss Raid Portfolio의 일일 개발 기록을 모아두는 인덱스 문서입니다.  
기능 구현, 버그 수정, 검증 결과, 문서 동기화 이력을 날짜 단위로 추적할 수 있도록 구성했습니다.

## 프로젝트 개요

- 프로젝트명: `Boss Raid Portfolio`
- 장르: 3D 보스 레이드 액션 (싱글 + 멀티플레이 검증)
- 엔진: `Unity 2022.3.62f3`
- 언어: `C#`
- 핵심 설계 키워드: `FSM`, `Zero-GC`, `Network-Ready Input`, `Authoritative Multiplayer`
- 기록 범위: `2026-02-02` ~ `2026-05-01` (현재 저장소 기준)

## Progress Log를 쓰는 목적

1. 구현 내용과 의사결정 근거를 날짜별로 남깁니다.
2. 회귀 버그가 생겼을 때 변경 지점을 빠르게 추적합니다.
3. `System_Blueprint` / `Technical_Glossary` 업데이트의 근거 소스로 사용합니다.
4. 작업 완료 보고 시, 검증 결과(빌드/테스트/미실행 사유)를 명확히 남깁니다.

## 작성/운영 규칙

1. 로그 파일은 `YYYY-MM-DD.md` 형식으로 생성합니다.
2. 같은 날짜의 추가 작업은 새 파일을 만들지 않고 기존 날짜 파일에 병합합니다.
3. 신규 로그는 [TEMPLATE.md](/d:/Unity-projects/BossRaidPortfolio/docs/Progress_Log/TEMPLATE.md)를 기준으로 작성합니다.
4. `체크리스트 업데이트`와 `맥락노트`를 분리해 기록합니다.
5. `기술적 고려`에는 아래 3항목을 고정 포함합니다.
- 무엇을 발견했는가
- 무엇을 수정했는가
- 왜 그렇게 판단했는가

## 문서 동기화 규칙

1. 코드 변경 완료 후 문서 동기화 순서를 지킵니다.
- `docs/Progress_Log/YYYY-MM-DD.md` + 이 인덱스 파일
- `docs/technical/System_Blueprint.md`
- `docs/technical/Technical_Glossary.md`
2. 완료 보고에는 반드시 `참조 로그: docs/Progress_Log/YYYY-MM-DD.md`를 남깁니다.
3. 여러 날짜를 근거로 썼다면 참조 로그를 여러 줄로 명시합니다.

## 최근 작업 하이라이트

1. 멀티플레이 플레이어/보스 authority 경계 정리 및 HUD 동기화 안정화
2. 보스 공격 warning/telegraph 재생 타이밍 보정 및 replay 품질 개선
3. visual binding self-heal, blink/material fallback 등 presentation 회귀 복구
4. Title -> Loading -> Gameplay 흐름 및 scene/runtime root 계약 정리
5. 빌드 검증/문서 동기화를 포함한 유지보수 워크플로 강화

## 날짜별 로그

- [2026-05-12.md](/d:/Unity-projects/BossRaidPortfolio/docs/Progress_Log/2026-05-12.md) 
- [2026-05-01.md](/d:/Unity-projects/BossRaidPortfolio/docs/Progress_Log/2026-05-01.md)
- [2026-04-30.md](/d:/Unity-projects/BossRaidPortfolio/docs/Progress_Log/2026-04-30.md)
- [2026-04-29.md](/d:/Unity-projects/BossRaidPortfolio/docs/Progress_Log/2026-04-29.md)
- [2026-04-28.md](/d:/Unity-projects/BossRaidPortfolio/docs/Progress_Log/2026-04-28.md)
- [2026-04-20.md](/d:/Unity-projects/BossRaidPortfolio/docs/Progress_Log/2026-04-20.md)
- [2026-04-17.md](/d:/Unity-projects/BossRaidPortfolio/docs/Progress_Log/2026-04-17.md)
- [2026-04-15.md](/d:/Unity-projects/BossRaidPortfolio/docs/Progress_Log/2026-04-15.md)
- [2026-04-14.md](/d:/Unity-projects/BossRaidPortfolio/docs/Progress_Log/2026-04-14.md)
- [2026-04-13.md](/d:/Unity-projects/BossRaidPortfolio/docs/Progress_Log/2026-04-13.md)
- [2026-04-10.md](/d:/Unity-projects/BossRaidPortfolio/docs/Progress_Log/2026-04-10.md)
- [2026-04-09.md](/d:/Unity-projects/BossRaidPortfolio/docs/Progress_Log/2026-04-09.md)
- [2026-04-08.md](/d:/Unity-projects/BossRaidPortfolio/docs/Progress_Log/2026-04-08.md)
- [2026-04-07.md](/d:/Unity-projects/BossRaidPortfolio/docs/Progress_Log/2026-04-07.md)
- [2026-04-06.md](/d:/Unity-projects/BossRaidPortfolio/docs/Progress_Log/2026-04-06.md)
- [2026-04-03.md](/d:/Unity-projects/BossRaidPortfolio/docs/Progress_Log/2026-04-03.md)
- [2026-04-02.md](/d:/Unity-projects/BossRaidPortfolio/docs/Progress_Log/2026-04-02.md)
- [2026-04-01.md](/d:/Unity-projects/BossRaidPortfolio/docs/Progress_Log/2026-04-01.md)
- [2026-03-31.md](/d:/Unity-projects/BossRaidPortfolio/docs/Progress_Log/2026-03-31.md)
- [2026-03-30.md](/d:/Unity-projects/BossRaidPortfolio/docs/Progress_Log/2026-03-30.md)
- [2026-03-27.md](/d:/Unity-projects/BossRaidPortfolio/docs/Progress_Log/2026-03-27.md)
- [2026-03-26.md](/d:/Unity-projects/BossRaidPortfolio/docs/Progress_Log/2026-03-26.md)
- [2026-03-25.md](/d:/Unity-projects/BossRaidPortfolio/docs/Progress_Log/2026-03-25.md)
- [2026-03-24.md](/d:/Unity-projects/BossRaidPortfolio/docs/Progress_Log/2026-03-24.md)
- [2026-03-19.md](/d:/Unity-projects/BossRaidPortfolio/docs/Progress_Log/2026-03-19.md)
- [2026-03-18.md](/d:/Unity-projects/BossRaidPortfolio/docs/Progress_Log/2026-03-18.md)
- [2026-03-17.md](/d:/Unity-projects/BossRaidPortfolio/docs/Progress_Log/2026-03-17.md)
- [2026-03-16.md](/d:/Unity-projects/BossRaidPortfolio/docs/Progress_Log/2026-03-16.md)
- [2026-03-13.md](/d:/Unity-projects/BossRaidPortfolio/docs/Progress_Log/2026-03-13.md)
- [2026-03-12.md](/d:/Unity-projects/BossRaidPortfolio/docs/Progress_Log/2026-03-12.md)
- [2026-03-11.md](/d:/Unity-projects/BossRaidPortfolio/docs/Progress_Log/2026-03-11.md)
- [2026-03-06.md](/d:/Unity-projects/BossRaidPortfolio/docs/Progress_Log/2026-03-06.md)
- [2026-03-05.md](/d:/Unity-projects/BossRaidPortfolio/docs/Progress_Log/2026-03-05.md)
- [2026-03-04.md](/d:/Unity-projects/BossRaidPortfolio/docs/Progress_Log/2026-03-04.md)
- [2026-03-03.md](/d:/Unity-projects/BossRaidPortfolio/docs/Progress_Log/2026-03-03.md)
- [2026-02-28.md](/d:/Unity-projects/BossRaidPortfolio/docs/Progress_Log/2026-02-28.md)
- [2026-02-27.md](/d:/Unity-projects/BossRaidPortfolio/docs/Progress_Log/2026-02-27.md)
- [2026-02-26.md](/d:/Unity-projects/BossRaidPortfolio/docs/Progress_Log/2026-02-26.md)
- [2026-02-24.md](/d:/Unity-projects/BossRaidPortfolio/docs/Progress_Log/2026-02-24.md)
- [2026-02-23.md](/d:/Unity-projects/BossRaidPortfolio/docs/Progress_Log/2026-02-23.md)
- [2026-02-21.md](/d:/Unity-projects/BossRaidPortfolio/docs/Progress_Log/2026-02-21.md)
- [2026-02-20.md](/d:/Unity-projects/BossRaidPortfolio/docs/Progress_Log/2026-02-20.md)
- [2026-02-12.md](/d:/Unity-projects/BossRaidPortfolio/docs/Progress_Log/2026-02-12.md)
- [2026-02-11.md](/d:/Unity-projects/BossRaidPortfolio/docs/Progress_Log/2026-02-11.md)
- [2026-02-10.md](/d:/Unity-projects/BossRaidPortfolio/docs/Progress_Log/2026-02-10.md)
- [2026-02-09.md](/d:/Unity-projects/BossRaidPortfolio/docs/Progress_Log/2026-02-09.md)
- [2026-02-06.md](/d:/Unity-projects/BossRaidPortfolio/docs/Progress_Log/2026-02-06.md)
- [2026-02-05.md](/d:/Unity-projects/BossRaidPortfolio/docs/Progress_Log/2026-02-05.md)
- [2026-02-04.md](/d:/Unity-projects/BossRaidPortfolio/docs/Progress_Log/2026-02-04.md)
- [2026-02-03.md](/d:/Unity-projects/BossRaidPortfolio/docs/Progress_Log/2026-02-03.md)
- [2026-02-02.md](/d:/Unity-projects/BossRaidPortfolio/docs/Progress_Log/2026-02-02.md)

## 관련 문서

- 템플릿: [TEMPLATE.md](/d:/Unity-projects/BossRaidPortfolio/docs/Progress_Log/TEMPLATE.md)
- 장기 백로그: [Milestone_Backlog.md](/d:/Unity-projects/BossRaidPortfolio/docs/roadmap/Milestone_Backlog.md)
- 레거시 단일 로그: [LEGACY_MONOLITH.md](/d:/Unity-projects/BossRaidPortfolio/docs/Progress_Log/LEGACY_MONOLITH.md)
