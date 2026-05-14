# Artifact Bundles

이 프로젝트의 대용량 아트 리소스는 Git/LFS에 직접 올리지 않고 외부 zip 번들로 관리합니다.

## 다운로드 위치

- Google Drive: [Artifact Bundles 폴더](https://drive.google.com/drive/folders/1tUFzeVkNQiX1_ts8eaDFry1dULgp9XDp?usp=drive_link)
- 현재 검증된 번들: `BossRaidPortfolio_RequiredArt-20260514-202854.zip`
- SHA256: `5DCA481F59CBD79D2395B2FA2BB0DDFECC69EB9A45D813D51F6C2C47A734BD32`

## 복원 방법

1. Google Drive에서 `BossRaidPortfolio_RequiredArt-*.zip` 파일과 같은 이름의 `.sha256` 파일을 내려받습니다.
2. 두 파일을 프로젝트 폴더 밖에 둡니다. 기본 위치는 아래 폴더입니다.

```text
D:\Unity-projects\BossRaidPortfolio_ArtifactBundles\
```

3. 프로젝트 루트에서 복원 스크립트를 실행합니다. 기본 위치에 번들이 하나만 있으면 `-ArchivePath`를 생략할 수 있습니다.

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File .\tools\artifacts\restore-assets.ps1
```

4. 특정 zip을 직접 지정하려면 아래처럼 실행합니다.

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File .\tools\artifacts\restore-assets.ps1 -ArchivePath "D:\Unity-projects\BossRaidPortfolio_ArtifactBundles\BossRaidPortfolio_RequiredArt-20260514-202854.zip"
```

`restore-assets.ps1`는 같은 위치의 `.sha256` 파일을 자동으로 읽어 해시를 검증합니다. 복원 후에는 `verify-assets.ps1`를 자동 실행해서 필수 경로가 존재하는지 확인합니다.

## 수동 검증

복원 뒤 아래 명령으로 아트 번들과 Git에 남아야 하는 경량 파일이 모두 있는지 다시 확인할 수 있습니다.

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File .\tools\artifacts\verify-assets.ps1
```

성공하면 아래와 비슷한 출력이 나옵니다.

```text
Artifact paths present: 18
Git required paths present: 5
```

## 새 번들 만들기

아트 리소스를 갱신했다면 프로젝트 루트에서 아래 명령을 실행합니다.

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File .\tools\artifacts\package-assets.ps1 -OutputDirectory "D:\Unity-projects\BossRaidPortfolio_ArtifactBundles"
```

스크립트는 새 zip과 `.sha256` 파일을 생성합니다. 생성된 두 파일을 Google Drive의 Artifact Bundles 폴더에 업로드합니다.

## Git에 넣는 것과 넣지 않는 것

Git에 넣습니다:

- 씬, 프리팹, 스크립트, 애니메이터 컨트롤러
- `tools/artifacts/*.ps1`
- `tools/artifacts/artifact-manifest.json`

Git에 넣지 않습니다:

- `BossRaidPortfolio_RequiredArt-*.zip`
- `.sha256`
- `Assets/Modelings`, `Assets/Map`, `Assets/CombatGirlsCharacterPack` 같은 대용량 외부 아트 폴더
