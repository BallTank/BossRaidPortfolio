# 🛠️ AI Guide Chapter 02: Execution, Roles, Quality

이 문서는 승인된 계획을 실제 변경으로 옮길 때의 실행 절차와 역할 분리, 완료 보고 기준을 정의합니다.

## 1. 실행 프로토콜

### [Step 0] Hook 확인
작업 유형에 맞는 선행 문서를 확인하고 현재 규칙/아키텍처를 먼저 파악한다.

### [Step 1] 계획 제출
`System_Blueprint` 기준으로 계획서를 작성하고 사용자 승인을 받는다.

### [Step 2] 구현 (Implementer)
승인된 범위 안에서 변경한다. 승인되지 않은 범위 확장은 금지한다.

### [Step 3] 셀프 검토 (QA Reviewer)
구현 직후 `Coding_Standard.md` 기준으로 규칙 준수 여부를 체크한다.
코드 신규 작성/수정이 있으면 컴파일/빌드 기반 코드 검사를 최소 1회 실행한다.
예: `dotnet build Assembly-CSharp.csproj -v:minimal`
검사를 수행하지 못한 경우에는 미실행 사유와 대체 검증을 완료 보고에 남긴다.
코드 검사 결과 확인 전에는 작업 완료 보고와 체크리스트 완료(`[x]`) 처리를 금지한다.

### [Step 4] 문서 동기화
코드 변경 후 아래 순서로 문서를 갱신한다.
1. `docs/Progress_Log/YYYY-MM-DD.md` (당일 로그 작성) + `docs/Progress_Log/README.md` (인덱스 확인/갱신)
2. `docs/technical/System_Blueprint.md`
3. `docs/technical/Technical_Glossary.md`

#### Progress_Log Tracker Pass
문서 동기화 시에는 기준 로그 파일을 먼저 지정하고 아래 매핑으로 반영한다.

| 동기화 대상 | Progress_Log 근거 섹션 | 반영 예시 |
| --- | --- | --- |
| `docs/technical/System_Blueprint.md` | `오늘 반영한 작업`, `체크리스트 업데이트` | 구현 현황표 갱신, 규칙 문구 보강, 다이어그램 설명 보완 |
| `docs/technical/Technical_Glossary.md` | `기술적 고려`, `맥락노트` | 신규 용어 등록, 정의 문장 보강, 용어명 정규화 |

완료 보고에는 `참조 로그: docs/Progress_Log/YYYY-MM-DD.md`를 반드시 기록한다.

## 2. Agent Roles

| 역할 | 핵심 책임 | 완료 기준 |
| --- | --- | --- |
| **Implementer** | 승인된 계획 구현, 영향 파일 수정, 문서 반영 준비 | 계획 범위 내 구현 + 변경 근거 정리 |
| **QA Reviewer** | 규칙 준수/회귀 위험/문서 정합성 자체 점검 | `Coding_Standard` 체크 통과 + 문서 동기화 완료 |

## 3. QA Reviewer 체크리스트

* `Update`/`FixedUpdate`/`LateUpdate` 루프에서 불필요한 할당이 없는가.
* 입력/로직 분리 규칙(`Input Provider -> Packet -> State`)을 위반하지 않았는가.
* 상태 전환/공격 판정/이벤트 구독 해제 등 회귀 위험 지점이 보완되었는가.
* 변경 내용이 `System_Blueprint` 다이어그램/현황표와 모순되지 않는가.
* 신규 용어/정책이 `Technical_Glossary`에 반영되었는가.
* 코드 변경 후 컴파일/빌드 기반 코드 검사 결과(성공/경고/실패)가 기록되었는가.
* 코드 검사 전에 완료 보고/체크리스트 완료(`[x]`)를 처리하지 않았는가.
* 완료 보고에 `참조 로그: docs/Progress_Log/YYYY-MM-DD.md`가 명시되었는가.

## 4. 작업 완료 품질 보고 템플릿

`docs/Progress_Log/YYYY-MM-DD.md`의 `기술적 고려` 섹션은 아래 3항목을 항상 포함한다.

```md
* **기술적 고려**
* **무엇을 발견했는가**
* **무엇을 수정했는가**
* **왜 그렇게 판단했는가**
```

## 5. Progress_Log 기록 분리 규칙

신규 엔트리는 아래 2개 블록을 분리해 작성한다.

1. `체크리스트 업데이트`: 완료/진행/보류 항목을 체크박스로 기록
2. `맥락노트`: 선택한 접근, 제외한 대안, 판단 근거를 간단히 기록
3. `코드 검사 결과`: 코드 변경이 있을 때 실행 명령/결과/미실행 사유를 기록

### 권장 엔트리 형태

```md
### **YYYY-MM-DD (요일): 작업 제목**
* **오늘 반영한 작업**
* ...

* **체크리스트 업데이트**
* [x] ...
* [ ] ...

* **맥락노트**
* ...

* **코드 검사 결과 (코드 변경 시 필수)**
* 명령: `dotnet build Assembly-CSharp.csproj -v:minimal`
* 결과: 성공/실패 (Warning N, Error N)
* 미실행 사유 및 대체 검증: (미실행 시 필수)

* **기술적 고려**
* **무엇을 발견했는가**
* **무엇을 수정했는가**
* **왜 그렇게 판단했는가**
```


