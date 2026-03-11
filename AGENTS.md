# AGENTS.md

이 저장소는 AI 보조 개발 시 문서 우선 워크플로를 사용합니다.

## 소유자 승인 규칙

- 코드, 문서 등 프로젝트에 있는 모든 파일을 작성 또는 수정 전 주인에게 허락맡기

## 링크 표기 규칙

- 코드 라인/파일 경로 안내는 VS Code에서 바로 열리는 로컬 경로 형식으로 작성한다.
- `file+.vscode-resource.vscode-cdn.net` 같은 웹뷰 URL 형식은 사용하지 않는다.
- 파일 링크는 `[파일명](/절대/경로/파일:라인)` 또는 상대 경로(`Assets/...:라인`)를 우선 사용한다.
- 에이전트 응답에서는 하이퍼링크 실패를 방지하기 위해 `Assets/...:라인` 형식의 plain text 표기를 기본값으로 사용한다.

## 필수 컨텍스트

중요한 구현 작업을 시작하기 전에 아래 문서를 읽습니다:

1. `docs/AI_Maintenance_Guide.md`
2. `docs/technical/System_Blueprint.md`
3. `docs/technical/Input_FSM_Flow.md`
4. `docs/technical/Coding_Standard.md`

## 코드 변경 후 필수 동기화

코드 변경 완료 후에는 아래 순서로 문서를 업데이트합니다:

1. `docs/Progress_Log/YYYY-MM-DD.md` (당일 로그 작성) + `docs/Progress_Log/README.md` (인덱스 확인/갱신)
2. `docs/technical/System_Blueprint.md` (구현 현황 표 및 관련 다이어그램)
3. `docs/technical/Technical_Glossary.md`

## 문서화 규칙

- 기존 문서 형식(헤딩, 표, Mermaid 블록)을 유지합니다.
- 코드 주석은 한국어로 작성합니다.
- 문서는 한국어를 기본으로 작성하되, 로직 설명(상태 전환/알고리즘/흐름)은 쉬운 영어로 작성합니다.
- 에이전트의 사용자 응답은 쉬운 영어로 작성합니다.
- `docs/AI_Maintenance_Guide.md`에 정의된 클래스 다이어그램/시각화 규칙을 따릅니다.

## 커밋 메시지 규칙

- 커밋 시 `summary`와 `description`은 반드시 한국어로 작성합니다.
- 커밋 실행 전 `summary`/`description` 초안을 사용자에게 먼저 확인받습니다.

