# AGENTS.md

이 저장소는 AI 보조 개발 시 문서 우선 워크플로를 사용합니다.

## 필수 컨텍스트

중요한 구현 작업을 시작하기 전에 아래 문서를 읽습니다:

1. `docs/AI_Maintenance_Guide.md`
2. `docs/technical/System_Blueprint.md`
3. `docs/technical/Input_FSM_Flow.md`
4. `docs/technical/Coding_Standard.md`

## 코드 변경 후 필수 동기화

코드 변경 완료 후에는 아래 순서로 문서를 업데이트합니다:

1. `docs/Progress_Log.md`
2. `docs/technical/System_Blueprint.md` (구현 현황 표 및 관련 다이어그램)
3. `docs/technical/Technical_Glossary.md`

## 문서화 규칙

- 기존 문서 형식(헤딩, 표, Mermaid 블록)을 유지합니다.
- 코드 주석은 반드시 한국어로 작성합니다.
- 문서 텍스트 업데이트는 한국어를 사용합니다.
- `docs/AI_Maintenance_Guide.md`에 정의된 클래스 다이어그램/시각화 규칙을 따릅니다.

## 커밋 메시지 규칙

- 커밋 시 `summary`와 `description`은 반드시 한국어로 작성합니다.
- 커밋 실행 전 `summary`/`description` 초안을 사용자에게 먼저 확인받습니다.
