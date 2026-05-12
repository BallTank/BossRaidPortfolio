# Unity Prompt Templates

Unity 프로젝트 작업 시 AI 요청 품질을 높이기 위한 프롬프트 템플릿 모음입니다.  
버그 분석/수정과 기능 설계/구현 요청을 분리하여, 문제 정의부터 검증까지 한 흐름으로 정리할 수 있도록 구성했습니다.

## 템플릿 개요

- 버그 대응용: [Unity_Bug_Fix_Request_Template.md](/d:/Unity-projects/BossRaidPortfolio/docs/prompts/Unity_Bug_Fix_Request_Template.md)
- 기능 추가용: [Unity_Feature_Request_Template.md](/d:/Unity-projects/BossRaidPortfolio/docs/prompts/Unity_Feature_Request_Template.md)

## 언제 무엇을 쓰나

1. `Unity_Bug_Fix_Request_Template`
- 재현 가능한 오류, 회귀, 예외 로그, 특정 상태에서만 발생하는 오동작을 다룰 때 사용합니다.
- Root-cause 가설 -> 검증 -> 최소 수정안 순서로 요청할 수 있습니다.

2. `Unity_Feature_Request_Template`
- 새 기능 추가, 기존 시스템 확장, UX/연출 개선을 계획할 때 사용합니다.
- Flow 정의 -> 영향 시스템 확인 -> 안전한 구현 계획 수립 순서로 요청할 수 있습니다.

## 사용 방법

1. 작업 목적에 맞는 템플릿 1개를 선택합니다.
2. 템플릿 전체를 복사해 채팅 입력창에 붙여넣습니다.
3. 현재 알고 있는 정보만 먼저 작성합니다.
4. 모르는 항목은 `TBD`로 남겨도 됩니다.
5. 먼저 `No code yet` 단계로 진단/계획을 받고, 이후 `Code Request`로 구현을 요청합니다.

## 작성 팁

- 재현 스텝은 가능한 짧고 정확하게 씁니다.
- `Things that must not break`는 반드시 작성합니다.
- `Pass condition`을 명확히 쓰면 완료 기준이 흔들리지 않습니다.
- 콘솔 로그/에러 전문은 원문 그대로 붙이는 것이 좋습니다.

## 빠른 워크플로

1. 템플릿 작성
2. AI에 1차 분석(진단/계획) 요청
3. 분석 결과 확인 후 구현 요청
4. Unity에서 테스트 결과 기록
5. 같은 템플릿의 `After Testing` 섹션으로 후속 수정 요청
