# Rollback Snapshots

## 2026-03-03 Lunge Experiment (before rollback)

- Patch: `docs/rollback_snapshots/2026-03-03_lunge_experiment_before_rollback.patch`
- Raw files:
  - `docs/rollback_snapshots/2026-03-03_lunge_experiment_before_rollback_files/BossController.cs`
  - `docs/rollback_snapshots/2026-03-03_lunge_experiment_before_rollback_files/BossVisual.cs`
  - `docs/rollback_snapshots/2026-03-03_lunge_experiment_before_rollback_files/LungeAttackPattern.cs`

## Re-apply 방법

### 방법 1) patch 적용
```powershell
git apply --reject --whitespace=nowarn docs/rollback_snapshots/2026-03-03_lunge_experiment_before_rollback.patch
```

### 방법 2) 파일 단위 복원
필요 파일만 수동 비교 후 교체:
- `Assets/Scripts/Boss/BossController.cs`
- `Assets/Scripts/Boss/BossVisual.cs`
- `Assets/Scripts/Boss/Attacks/LungeAttackPattern.cs`

## 주의

- 현재 워킹트리에 변경 파일이 있으면 patch 충돌이 날 수 있다.
- 적용 후 반드시 `dotnet build BossRaidPortfolio.sln`로 컴파일 확인한다.
