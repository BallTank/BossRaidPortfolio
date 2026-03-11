# 🤖 AI Documentation Maintenance Guide

이 문서는 프로젝트 `Boss Raid Portfolio`의 AI 보조 개발을 위한 **인덱스(요약 규칙)**입니다.  
상세 규칙은 `docs/maintenance/` 챕터 문서로 분리해 유지합니다.

## 1. Core Mission

모든 코드 변경 사항은 반드시 관련 문서(`Blueprint`, `Standard`, `Glossary`, `Log`)에 반영되어야 합니다.  
코드가 진화함에 따라 문서가 뒤처지는 '문서화 부채'를 방지하는 것이 AI의 최우선 임무입니다.

## 2. Quick Index (능동 Hook + Plan-First)

| 작업 유형 | 선행 Hook 문서 (최소) | 추가 확인 | 실행 게이트 |
| --- | --- | --- | --- |
| 신규 기능 구현/구조 변경 | `docs/technical/System_Blueprint.md` | `docs/technical/Coding_Standard.md` | **계획서 작성 -> 사용자 승인 -> 구현** |
| API/입력/FSM 관련 작업 | `docs/technical/Input_FSM_Flow.md` | `docs/technical/System_Blueprint.md` | 설계 구조 확인 전 답변/구현 금지 |
| 성능/물리/풀링 최적화 | `docs/technical/Coding_Standard.md` | `docs/technical/System_Blueprint.md` | Zero-GC/NonAlloc 규칙 충돌 검증 필수 |
| 애니메이션/비주얼 변경 | `docs/animation/Animator_Setup_Guide.md` | `docs/animation/Animation_Implementation_Log.md` | 기존 리소스 계약(이벤트/파라미터) 점검 후 진행 |
| 버그 수정/회귀 대응 | `docs/Progress_Log/README.md`(최신 일자 파일 확인) | 관련 기술 문서 1개 이상 | 원인 가설 + 검증 계획을 먼저 제시 |

> Hook 판정이 애매하면 기본 세트(`System_Blueprint`, `Input_FSM_Flow`, `Coding_Standard`, `Progress_Log`)를 먼저 읽는다.

## 3. Mandatory Workflow (강제)

### [Step 0] Hook 판정
요청 유형을 분류하고 위 Quick Index에 맞는 선행 문서를 먼저 읽는다.

### [Step 1] Plan-First
신규 기능/구조 변경/비용이 큰 수정은 구현 전에 계획서를 작성한다.  
계획서에는 최소 `목표`, `영향 파일`, `리스크`, `검증 시나리오`, `문서 동기화 대상`이 포함되어야 한다.

### [Step 2] Approval Gate
사용자 승인 전에는 코드/문서를 수정하지 않는다.

### [Step 3] Implementation (Implementer 역할)
승인된 계획서 범위 안에서 구현한다. 계획서와 다른 판단이 필요하면 먼저 변경 근거를 제시하고 재승인을 받는다.

### [Step 4] Self-Review (QA Reviewer 역할)
구현 직후 `Coding_Standard.md` 준수 여부를 셀프 체크한다.
코드 신규 작성/수정이 포함된 작업은 최소 1회 컴파일/빌드 기반 코드 검사를 필수로 수행한다.
검사를 실행하지 못한 경우, 미실행 사유와 대체 검증 내용을 작업 보고에 명시한다.
코드 검사 결과 확인 전에는 완료 보고 및 체크리스트 완료(`[x]`) 처리를 금지한다.
`docs/` 파일을 수정한 경우에는 `Coding_Standard.md`의 Korean Integrity Validation(한글 깨짐 검증)까지 통과해야 완료로 간주한다.

### [Step 5] Document Sync (필수)
코드 변경 후 아래 순서로 동기화한다.
1. `docs/Progress_Log/YYYY-MM-DD.md` (당일 로그 작성) + `docs/Progress_Log/README.md` (인덱스 확인/갱신)
2. `docs/technical/System_Blueprint.md`
3. `docs/technical/Technical_Glossary.md`

### [Step 5-1] Progress_Log Tracker Pass (추적 패스)
다른 문서를 최신화할 때는 `Progress_Log`를 근거 소스로 고정한다.
1. `docs/Progress_Log/README.md`에서 기준 로그 파일(`docs/Progress_Log/YYYY-MM-DD.md`)을 선택한다.
2. 기준 로그의 `오늘 반영한 작업`, `체크리스트 업데이트`, `맥락노트`, `기술적 고려`를 근거로 `System_Blueprint`/`Technical_Glossary`를 갱신한다.
3. 완료 보고에는 `참조 로그: docs/Progress_Log/YYYY-MM-DD.md`를 남긴다.

## 4. Progress_Log 품질 보고 규칙

신규 로그부터 `체크리스트 업데이트`와 `맥락노트`를 분리해 작성한다.  
`기술적 고려`에는 아래 3항목을 고정 포함한다.

1. **무엇을 발견했는가**
2. **무엇을 수정했는가**
3. **왜 그렇게 판단했는가**

장기 백로그(마일스톤/버그/폴리싱)는 `docs/roadmap/Milestone_Backlog.md`에서 관리한다.

### [동기화 추적 규칙]
다른 문서를 업데이트하거나 추적할 때, 인덱스 링크만 남기지 말고 기준 일자 로그 파일까지 함께 명시한다.

### [1일 1로그 원칙]
동일 날짜에는 `Progress_Log` 엔트리를 1개만 유지한다.  
같은 날 추가 작업은 새 날짜 헤더를 만들지 않고, 기존 날짜 엔트리 내부 소제목/항목으로 병합한다.

## 5. Agent Roles

| 역할 | 목적 | 필수 체크 |
| --- | --- | --- |
| **Implementer** | 승인된 계획을 코드/문서 변경으로 구현 | 계획 범위 준수, 영향 파일 반영 |
| **QA Reviewer** | 구현 직후 품질 검토 | `Coding_Standard.md` 준수, 회귀 리스크, 문서 동기화 완료 |

## 6. Detailed Chapters (상세 규칙)

1. `docs/maintenance/AI_Guide_01_Hook_and_PlanFirst.md`
2. `docs/maintenance/AI_Guide_02_Execution_Roles_Quality.md`
3. `docs/maintenance/AI_Guide_03_Diagram_Encoding_Logging.md`

## 7. Constraints

* 문서 업데이트 시 기존의 포맷(Headings, Tables, Mermaid)을 엄격히 유지한다.
* 주석은 한국어를 사용한다.
* 문서는 한국어를 기본으로 작성하되, 로직 설명(상태 전환/알고리즘/흐름)은 쉬운 영어를 사용한다.
* 에이전트의 사용자 응답은 쉬운 영어를 사용한다.
* `docs/` 문서는 UTF-8(UTF-8 with BOM 허용)으로 유지한다.
* 클래스 다이어그램/시각화 규칙은 Chapter 03을 따른다.
* 파일 경로 링크는 VS Code 로컬 경로 형식을 사용하고, 웹뷰 URL(`file+.vscode-resource.vscode-cdn.net`)은 사용하지 않는다.
* 링크 타겟은 Windows 백슬래시 경로(`d:\...`) 대신 슬래시 절대경로(`/d:/...`)를 사용하고, 라인 점프는 `:라인`을 링크 텍스트가 아니라 링크 타겟 내부에 포함한다.
* 에이전트 응답의 기본 파일 참조 표기는 하이퍼링크 대신 `Assets/...:라인` plain text 형식을 우선 사용한다.

## 8. Code Reading Rule (6-Line Trace Card)

코드 이해/버그 분석 요청에서는 전체 파일 정독보다 `6-Line Trace Card`를 우선 작성한다.

### 8.1. Card Format (고정 6줄)
1. `Trigger`: 무엇이 시작점인가
2. `Entry`: 첫 호출 함수는 무엇인가
3. `Gate`: 상태/타이머/조건 게이트는 무엇인가
4. `Core Check`: 실제 물리/수학 판정은 무엇인가
5. `Effect`: 데미지/상태 변경 호출은 무엇인가
6. `Result`: 플레이어 체감 결과(HP, 스턴, 무시)는 무엇인가

각 줄은 아래 형식을 사용한다.
`[S#] Action | Condition | File:line | Key value`

### 8.2. Writing Policy
* 로직 설명은 쉬운 영어로 작성한다.
* 한 카드에서 다루는 동작은 1개만 선택한다 (예: Attack4 AoE hit).
* 카드 작성 후 필요한 줄만 상세 디버깅한다 (breakpoint/watch value).

