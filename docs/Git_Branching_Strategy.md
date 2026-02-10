# Git 브랜치 전략 (Boss Raid Portfolio)

## 개요
이 프로젝트는 기능별로 브랜치를 나누어 개발하는 **Gitflow 변형 모델**을 사용합니다. 이를 통해 **Player**, **Boss**, **Common** 등 각 시스템을 독립적으로 관리하며 안정적인 개발 환경을 유지합니다.

## 브랜치 구조

### 1. 메인 브랜치 (Main Branches)
프로젝트의 전체 수명 동안 유지되는 핵심 브랜치입니다.

- **`main` (또는 `master`)**:
  - **용도**: 언제든지 배포 및 시연 가능한 안정적인 버전.
  - **규칙**: 직접 커밋 금지. `develop` 브랜치나 `hotfix` 브랜치에서만 병합(Merge) 가능.
  - **활용**: 포트폴리오 제출용 빌드.

- **`develop`**:
  - **용도**: 다음 배포를 위해 기능을 통합하는 브랜치.
  - **규칙**: 기능 브랜치에서 완료된 작업이 이곳으로 병합됨.
  - **활용**: 일일 개발의 기준점(Base).

### 2. 기능 브랜치 (Feature Branches)
새로운 기능 개발이나 리팩토링을 위해 임시로 생성하는 브랜치입니다.

- **명명 규칙**: `feature/<카테고리>/<설명>`
  - **카테고리 예시**:
    - `player/`: 플레이어 이동, 공격, 상태 머신 업데이트 등
    - `boss/`: 보스 AI 패턴, 페이즈 전환 로직 등
    - `common/`: 공통 시스템, 유틸리티, 입력 시스템, UI 등
    - `level/`: 레벨 디자인, 환경 조명 등
  - **실제 예시**:
    - `feature/player/dash-implementation` (대시 구현)
    - `feature/boss/attack-pattern-1` (보스 공격 패턴 1)
    - `feature/common/fsm-refactoring` (FSM 리팩토링)

- **워크플로우**:
  1.  `develop` 브랜치로 체크아웃 및 최신화 (`git pull origin develop`)
  2.  새 브랜치 생성 (`git checkout -b feature/player/new-attack`)
  3.  작업 진행 및 커밋
  4.  작업 완료 후 `develop`으로 병합
  5.  기능 브랜치 삭제

### 3. 유지보수 브랜치 (Maintenance Branches - 선택 사항)
특정 버그 수정이나 코드 정리를 위한 브랜치입니다.

- **`fix/...`**: 버그 수정 (예: `fix/player/stuck-in-wall`)
- **`refactor/...`**: 기능 변경 없는 코드 구조 개선 (예: `refactor/common/folder-structure`)
- **`docs/...`**: 문서 업데이트 (예: `docs/api-guide`)

## 작업 순서 예시

### 1단계: 브랜치 초기화 (최초 1회)
`develop` 브랜치가 없다면 생성합니다.
```bash
git checkout main
git checkout -b develop
git push -u origin develop
```

### 2단계: 플레이어 콤보 기능 개발 시나리오
```bash
# 1. 작업 시작 (develop 브랜치에서 파생)
git checkout develop
git checkout -b feature/player/combo-system

# ... 코드 수정 및 커밋 ...
git add .
git commit -m "feat: Implement basic combo attack logic"

# 2. 작업 완료 및 병합
git checkout develop
git pull origin develop  # 원격 저장소의 최신 변경사항 반영 (충돌 방지)
git merge feature/player/combo-system

# 3. 원격 저장소 업로드 및 브랜치 정리
git push origin develop
git branch -d feature/player/combo-system
```

## 이 전략의 장점
- **격리성**: 보스 AI를 수정하다가 실수하더라도, 플레이어 이동 로직에는 영향을 주지 않습니다.
- **구조적 명확성**: `Player`, `Boss`, `Common` 폴더 구조와 브랜치 카테고리가 일치하여 직관적입니다.
- **안정적인 데모**: `main` 브랜치는 항상 깨끗하게 유지되므로, 언제든 교수님이나 면접관에게 보여줄 수 있습니다.
