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
| 버그 수정/회귀 대응 | `docs/Progress_Log.md`(최신 항목) | 관련 기술 문서 1개 이상 | 원인 가설 + 검증 계획을 먼저 제시 |

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

### [Step 5] Document Sync (필수)
코드 변경 후 아래 순서로 동기화한다.
1. `docs/Progress_Log.md`
2. `docs/technical/System_Blueprint.md`
3. `docs/technical/Technical_Glossary.md`

## 4. Progress_Log 품질 보고 규칙

신규 로그부터 `체크리스트 업데이트`와 `맥락노트`를 분리해 작성한다.  
`기술적 고려`에는 아래 3항목을 고정 포함한다.

1. **무엇을 발견했는가**
2. **무엇을 수정했는가**
3. **왜 그렇게 판단했는가**

장기 백로그(마일스톤/버그/폴리싱)는 `docs/roadmap/Milestone_Backlog.md`에서 관리한다.

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
* 주석 및 문서 텍스트는 한국어를 사용한다.
* `docs/` 문서는 UTF-8(UTF-8 with BOM 허용)으로 유지한다.
* 클래스 다이어그램/시각화 규칙은 Chapter 03을 따른다.
* 파일 경로 링크는 VS Code 로컬 경로 형식을 사용하고, 웹뷰 URL(`file+.vscode-resource.vscode-cdn.net`)은 사용하지 않는다.
