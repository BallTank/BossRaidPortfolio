# 🧭 AI Guide Chapter 01: Hook & Plan-First

이 문서는 작업 시작 전에 어떤 문서를 먼저 읽어야 하는지(능동 Hook), 그리고 구현 전 계획 승인(Plan-First)을 어떻게 강제할지 정의합니다.

## 1. 능동 Hook 규칙

| 요청 신호 | 반드시 먼저 읽을 문서 | 추가 확인 문서 | 시작 조건 |
| --- | --- | --- | --- |
| API/입력/FSM 관련 변경 | `docs/technical/Input_FSM_Flow.md` | `docs/technical/System_Blueprint.md` | 데이터 흐름/상태 전환 경로 확인 후 시작 |
| 신규 기능/구조 변경 | `docs/technical/System_Blueprint.md` | `docs/technical/Coding_Standard.md` | 계획서 승인 후 시작 |
| 성능/메모리/물리 이슈 | `docs/technical/Coding_Standard.md` | `docs/technical/System_Blueprint.md` | Zero-GC/NonAlloc 규칙 충돌 없음 확인 |
| UI/HUD 흐름 변경 | `docs/technical/System_Blueprint.md`(UI 섹션) | `docs/Progress_Log/README.md` 최신 일자 파일 | 이벤트 기반 갱신 경로 확인 후 시작 |
| 애니메이션 이벤트/클립 변경 | `docs/animation/Animator_Setup_Guide.md` | `docs/animation/Animation_Implementation_Log.md` | 파라미터/이벤트 계약 확인 후 시작 |
| 버그 수정/회귀 대응 | `docs/Progress_Log/README.md`(최신 일자 파일) | 관련 기술 문서 1개 이상 | 원인 가설과 검증 방법 작성 후 시작 |

## 2. Hook 실행 순서

1. 요청을 기능 유형으로 분류한다.
2. 표의 `반드시 먼저 읽을 문서`를 우선 확인한다.
3. 작업 시작 전에 현재 이해한 맥락을 3줄 이내로 요약한다.
4. 분류가 애매하면 기본 세트(`System_Blueprint`, `Input_FSM_Flow`, `Coding_Standard`, `Progress_Log`)를 먼저 확인한다.
5. 문서 최신화/추적 작업이면 `docs/Progress_Log/README.md`에서 기준 로그(`docs/Progress_Log/YYYY-MM-DD.md`)를 확정해 계획서에 먼저 기록한다.

## 3. Plan-First 강제 규칙

신규 기능 구현, 구조 리팩터링, 다중 파일 수정은 구현 전에 계획서를 제출한다.

### 계획서 최소 템플릿

```md
### 구현 계획서
- 목표:
- 범위(포함/제외):
- 영향 파일:
- 설계 근거(System_Blueprint 참조):
- 리스크(회귀 가능성):
- 검증 계획(테스트/수동 시나리오):
- 참조 로그(Progress_Log): `docs/Progress_Log/YYYY-MM-DD.md`
- 문서 동기화 계획(Progress_Log / System_Blueprint / Technical_Glossary):
```

## 4. 승인 게이트

* 사용자 승인 전에는 코드/문서를 수정하지 않는다.
* 구현 범위가 계획서와 달라지면 변경 이유를 먼저 제시하고 재승인을 받는다.
* 승인 이후에도 리스크가 새로 발견되면 즉시 공유하고 실행 순서를 재조정한다.

## 5. 사용자 요청 예시

* `"Input_FSM_Flow.md를 먼저 읽고, 변경 계획서를 작성한 뒤 승인 받기 전에는 코드 수정하지 마."`
* `"System_Blueprint 기준으로 구현 계획서부터 써줘. 영향 파일/리스크/검증 시나리오 포함."`

