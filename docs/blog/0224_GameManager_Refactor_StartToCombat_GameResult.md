# [Portfolio] 게임 매니저 리팩토링: 시작→전투 전환, 승리/패배 UI, 재시작 입력

## 1. 개요

오늘 작업의 중심은 `게임 루프의 빈 구간`을 메우는 것이었다.  
기존에는 전투 HUD와 피격 피드백은 연결되어 있었지만, **씬 시작 흐름(시작→전투 진입)**, **전투 종료 흐름(승리/패배 판정)**, **재도전 입력(재시작)**이 한 덩어리로 정리되어 있지 않았다.

이번 리팩토링에서는 다음 세 가지를 우선 연결했다.

- 시작→전투 전환을 `Loading Scene` 경유 구조로 분리
- `GameManager`에서 승리/패배 판정과 결과 UI 표시 통합
- `Enter` 입력으로 현재 씬 재시작

---

## 2. 시작→전투 전환 구조

### 2.1. SceneLoader 도입

씬 전환 로직을 static 유틸리티(`SceneLoader`)로 분리했다.  
핵심 역할은 **목표 씬을 예약하고, 로딩 씬을 먼저 진입시키는 것**이다.

- 목적지 씬 ID 보관
- 전환 진행 중 중복 호출 차단
- `LoadingScene`을 경유해 실제 목적지 씬으로 이동

이 구조를 통해 씬 전환 책임이 각 컴포넌트에 흩어지지 않고, 단일 진입점으로 모이게 됐다.

### 코드 1) SceneLoader의 전환 시작

```csharp
// SceneLoader.cs
public static void Load(GameSceneId targetSceneId)
{
    if (_isTransitionInProgress)
    {
        return;
    }

    if (targetSceneId == GameSceneId.Loading)
    {
        Debug.LogWarning("SceneLoader.Load: Loading 씬을 목적지로 지정할 수 없습니다.");
        return;
    }

    _targetSceneId = targetSceneId;
    _hasPendingTarget = true;
    _isTransitionInProgress = true;

    SceneManager.LoadScene(GetSceneName(GameSceneId.Loading));
}
```

`_isTransitionInProgress` 가드로 중복 전환을 막고, 목적지는 내부에 보관한 뒤 로딩 씬으로 먼저 이동한다.

### 2.2. LoadingSceneController 연결

`LoadingSceneController`는 로딩 씬에서 실제 비동기 로드를 담당한다.

- `SceneManager.LoadSceneAsync`로 대상 씬 로드
- 진행률 슬라이더/텍스트 갱신
- 최소 표시 시간 이후 `allowSceneActivation = true`

즉, 전환 로직(`SceneLoader`)과 로딩 연출(`LoadingSceneController`)을 분리해 유지보수 포인트를 나눴다.

### 코드 2) 예약된 목적지 소비 + 비동기 로드 시작

```csharp
// LoadingSceneController.cs
private void BeginLoading()
{
    string targetSceneName;
    if (!SceneLoader.TryConsumeTargetScene(out targetSceneName))
    {
        targetSceneName = SceneLoader.GetSceneName(_fallbackScene);
    }

    _loadOperation = SceneManager.LoadSceneAsync(targetSceneName);
    if (_loadOperation == null)
    {
        SceneLoader.CancelPendingTransition();
        return;
    }

    _loadOperation.allowSceneActivation = false;
    UpdateProgressUI(0f);
}
```

### 코드 3) 최소 노출 시간 이후 씬 활성화

```csharp
// LoadingSceneController.cs
private void Update()
{
    if (_loadOperation == null) return;

    _displayTimer += Time.deltaTime;
    float normalizedProgress = Mathf.Clamp01(_loadOperation.progress / 0.9f);
    UpdateProgressUI(normalizedProgress);

    if (_isActivatingScene) return;
    if (_loadOperation.progress < 0.9f) return;
    if (_displayTimer < _minimumDisplayDuration) return;

    _isActivatingScene = true;
    SceneLoader.NotifyTransitionCompleted();
    _loadOperation.allowSceneActivation = true;
}
```

---

## 3. 승리/패배 판정과 결과 UI

### 3.1. GameManager의 판정 책임

`GameManager`는 플레이어/보스 `Health.OnDeath` 이벤트를 구독해 전투 종료 신호를 수집한다.  
이벤트 발생 후 내부 상태를 통해 게임오버를 1회만 확정하고, 결과를 `Victory` 또는 `Defeated`로 정리한다.

### 코드 4) 사망 이벤트 기반 결과 판정

```csharp
// GameManager.cs
private void LateUpdate()
{
    if (CurrentState != GameFlowState.InGame) return;
    if (_isGameOverResolved) return;
    if (!_playerDead && !_bossDead) return;

    if (_bossDead)
    {
        ResolveGameOver(GameResult.Victory);
        return;
    }

    ResolveGameOver(GameResult.Defeated);
}

private void ResolveGameOver(GameResult result)
{
    if (_isGameOverResolved) return;

    _isGameOverResolved = true;
    CurrentState = GameFlowState.GameOver;
    CurrentResult = result;

    bool isVictory = result == GameResult.Victory;
    ShowGameOverUI(isVictory ? _victoryText : _defeatedText);
}
```

### 3.2. 결과 UI 표시

결과 판정이 끝나면 게임오버 UI 루트와 결과 텍스트를 노출한다.

- 승리 문구: `Victory`
- 패배 문구: `Try Again? (Press Enter to Restart)`

텍스트를 `TextArea` 기반 직렬화 필드로 둬서 인스펙터에서 바로 수정할 수 있게 구성했다.

---

## 4. 재시작 입력 처리

게임오버 상태에서는 `Enter`(메인 + 숫자패드) 입력을 받아 현재 활성 씬을 다시 로드한다.  
여기서도 중복 로드 방지를 위해 내부 플래그로 재진입을 차단했다.

### 코드 5) Enter 재시작 + 중복 로드 차단

```csharp
// GameManager.cs
private void Update()
{
    if (CurrentState != GameFlowState.GameOver) return;
    if (_isSceneLoading) return;

    bool isRestartPressed = Input.GetKeyDown(_restartKey);
    if (_restartKey == KeyCode.Return)
    {
        isRestartPressed = isRestartPressed || Input.GetKeyDown(KeyCode.KeypadEnter);
    }

    if (!isRestartPressed) return;
    RestartCurrentScene();
}

private void RestartCurrentScene()
{
    if (_isSceneLoading) return;

    _isSceneLoading = true;
    Scene activeScene = SceneManager.GetActiveScene();
    SceneManager.LoadScene(activeScene.name);
}
```

정리하면 재시작 흐름은 다음 순서다.

1. 게임오버 상태 진입
2. 사용자 `Enter` 입력 감지
3. 현재 씬 이름 재로드

---

## 5. 구현 상태와 검증 범위

`Progress_Log` 기준으로 현재 상태는 아래와 같다.

- **게임 매니저(시작 → 전투 페이즈 전환)**: 구현 중 (`SceneLoader` + `LoadingSceneController` 경유 전투 진입 경로 구성), 테스트 미실시
- **게임 매니저(승리/패배)**: 구현 중 (`GameManager` 결과 판정/UI/재시작 입력 처리), 동시 쓰러질 때 테스트 미실시

즉, 아키텍처 연결은 완료 단계에 들어갔고, 실플레이 검증은 다음 확인 항목으로 남아 있다.

---

## 6. 정리

이번 작업은 화려한 기능 추가보다는, **게임이 시작해서 끝날 때까지의 흐름을 코드 구조로 정리하는 리팩토링**에 가까웠다.  
특히 `전환 관리(SceneLoader)`, `로딩 연출(LoadingSceneController)`, `결과 판정(GameManager)`로 역할을 나눈 덕분에, 이후 페이즈 전환 로직을 붙일 때도 충돌 범위를 줄일 수 있는 기반이 생겼다.
