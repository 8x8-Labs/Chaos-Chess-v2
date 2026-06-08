# 버전 & 릴리스 가이드

이 프로젝트의 버전 관리, 브랜치 전략, 릴리스 자동화, Unity 빌드 버전 각인 규칙을 정리한 문서입니다.

- 버전 체계: **SemVer** `vMAJOR.MINOR.PATCH` (정식 출시 전이라 `0.x` 유지)
- 릴리스 노트: **PR 단위**로 모으는 [release-drafter](https://github.com/release-drafter/release-drafter)가 자동 생성
- 빌드 버전: Git 태그가 **단일 진실 출처(single source of truth)**

---

## 1. 브랜치 구조 (Git Flow)

```
feature/* ─→ develop ──(기능 충분)──→ release/x.y.z ──(테스트·버그픽스)──→ main
                 ▲                         │  RC: vX.Y.Z-rc.N             │  정식: vX.Y.Z
                 └──────────── 버그픽스 back-merge ──────────────────────┘
```

| 브랜치 | 역할 | 버전 표기 |
| --- | --- | --- |
| `develop` | 기능 개발 누적 | 태그 없음 — 다음 버전 **draft 노트**만 갱신 |
| `release/x.y.z` | 테스트 중 버그 잡기 | **RC 프리릴리스** `vX.Y.Z-rc.N` (테스트 빌드용) |
| `main` | 정식 출시 | **정식 릴리스** `vX.Y.Z` + 태그 확정 |

- **develop**: PR이 머지될 때마다 다음 버전 릴리스 노트 초안에 추가됩니다(태그 없음). PR 하나가 노트 한 줄이라 내부 수정·변경도 그대로 기록됩니다.
- **release/x.y.z**: 출시마다 **새로 따는 일회용 브랜치**입니다. develop에서 기능이 충분히 모이면 `release/0.3.0` 식으로 컷하고, 테스트하며 버그픽스 PR을 올립니다. 머지될 때마다 `vX.Y.Z-rc.1 → rc.2 …` RC 프리릴리스가 publish됩니다(= 테스트 빌드 버전). 버그픽스는 **develop에도 back-merge**하세요. main 머지 후에는 브랜치를 삭제합니다.
- **main**: RC 검증이 끝나면 release 브랜치를 main으로 머지 → 정식 릴리스 `vX.Y.Z` publish + 태그 확정.

> **왜 고정 `release` 하나가 아니라 `release/x.y.z`인가?**
> 버전마다 브랜치를 따면 RC 버전(`0.3.0-rc.N`)이 명확하게 끊기고, `0.3.0` 출시 중에 `0.4.0`을 따로 준비할 수 있으며, Git Flow 표준과 일치합니다. 고정 브랜치 하나면 다음 사이클에서 버전 베이스와 RC 번호가 엉킵니다.

---

## 2. 릴리스 자동화 (release-drafter)

### 워크플로우

| 파일 | 트리거 | 동작 |
| --- | --- | --- |
| `.github/workflows/release-drafter.yml` | `develop` 푸시 + PR | 다음 버전 draft 노트 누적, PR 자동 라벨링 |
| `.github/workflows/release-rc.yml` | `release/**` 푸시 | **RC 프리릴리스** `vX.Y.Z-rc.N` publish |
| `.github/workflows/release-publish.yml` | `main` 푸시 | **정식** `vX.Y.Z` publish + 태그 확정 |

설정 파일: `.github/release-drafter.yml` (노트 양식 · 카테고리 · 라벨→버전 매핑 · autolabeler)

### 라벨 → 노트 분류 / 버전

PR에 붙은 **라벨**로 노트 카테고리와 다음 버전이 정해집니다. 라벨은 기존 커밋 컨벤션과 1:1로 매핑되며, PR 제목/브랜치 이름을 보고 **자동 부여**됩니다(autolabeler).

| 라벨 | 노트 섹션 | 버전 |
| --- | --- | --- |
| `feat` (제목 `feat:`/`add:`, `feature/*` 브랜치) | ✨ 새로운 기능 | minor |
| `fix` (제목 `fix:`, `fix/*` 브랜치) | 🐛 버그 수정 | patch |
| `modify` (제목 `modify:`) | 🔄 기능 수정 | patch |
| `refactor` (제목 `refactor:`) | ♻️ 리팩토링 | patch |
| `remove` (제목 `remove:`) | 🗑️ 삭제 | patch |
| `docs` (제목 `docs:`) | 📝 문서 | patch |
| `major` / `breaking` | — | major |

자동 라벨이 틀렸거나 비었으면 PR에서 라벨만 손으로 고치면 됩니다. `skip-changelog` 라벨을 붙이면 노트에서 제외됩니다.

---

## 3. Unity 빌드 버전 각인

`Assets/Editor/VersionStamp.cs`가 **빌드 직전** `git describe --tags`로 최신 태그를 읽어 `PlayerSettings.bundleVersion`에 자동 기입합니다. 화면 표시는 `Assets/Script/UI/VersionLabel.cs`(TMP 텍스트에 부착)가 `Application.version`을 띄웁니다.

브랜치별로 빌드 버전이 자연스럽게 구분됩니다:

| 빌드한 브랜치 | 최근 태그 예시 | 빌드 버전(`Application.version`) |
| --- | --- | --- |
| `release/0.3.0` (테스트) | `v0.3.0-rc.2` | `0.3.0-rc.2` (RC 위) / `0.3.0-rc.2+1.gabcd` (RC 이후 커밋) |
| `main` (정식) | `v0.3.0` | `0.3.0` |

---

## 4. 출시 사이클 (실전 흐름)

```
1. develop에서 기능 개발 (PR마다 노트 누적)
2. release/0.3.0 브랜치 컷 → 테스트 시작
3. 버그픽스 PR → release/0.3.0 머지 → rc.1, rc.2... 자동 publish
   (이 RC 빌드로 QA. 버그픽스는 develop에도 back-merge)
4. 검증 끝 → release/0.3.0을 main으로 머지 → 0.3.0 정식 릴리스 + 태그
5. release/0.3.0 브랜치 삭제
```

브랜치 컷 / 정리 명령:

```powershell
# (2) 출시 준비 시작 — develop에서 컷
git switch develop
git switch -c release/0.3.0
git push -u origin release/0.3.0

# (5) 검증·머지 완료 후 — 브랜치 삭제
git push origin --delete release/0.3.0
```

빌드 전에는 항상 최신 태그를 받아둡니다:

```powershell
git fetch --tags        # 또는 git pull
```

---

## 5. 최초 1회 셋업 (코드 외 작업)

릴리스 자동화가 동작하려면 아래를 한 번 준비해야 합니다.

1. **PR 라벨 생성** — autolabeler가 라벨을 부여하려면 라벨이 저장소에 미리 존재해야 합니다.
   ```powershell
   gh label create feat     --color 1D76DB
   gh label create fix      --color D73A4A
   gh label create modify   --color FBCA04
   gh label create refactor --color 0E8A16
   gh label create remove   --color B60205
   gh label create docs     --color 0075CA
   gh label create major    --color 5319E7
   gh label create skip-changelog --color CCCCCC
   ```
2. **`VersionLabel` 부착** — `MainScene`의 Canvas에 TMP 텍스트 오브젝트를 추가하고 `VersionLabel` 컴포넌트를 붙입니다(자동으로 붙지 않음).
3. **시작 태그** — 첫 릴리스 기준점을 잡습니다(예: `git tag v0.1.0`).

---

## 6. ⚠️ 주의사항

- **태그가 단일 진실 출처**입니다. `ProjectSettings.asset`의 버전을 손으로 고치지 마세요(release-drafter 계산값과 어긋남).
- **빌드 전 `git fetch --tags`**(또는 `git pull`)로 최신 태그를 받아야 정확한 버전이 각인됩니다.
- 정확히 태그 위에서 빌드하면 `0.3.1`, 태그 이후 커밋이 있는 빌드는 `0.3.1+2.g1a2b3c`처럼 커밋 정보가 붙어 정식 빌드가 아님이 구분됩니다.
- git이 없거나 실패하면 빌드를 막지 않고 기존 버전을 유지(경고만)합니다.
- release-drafter는 **PR 단위**로 노트를 모읍니다. 작업을 PR로 나눠 올리고 라벨이 맞는지 확인하세요(대부분 autolabeler가 자동 처리).
- `release-rc.yml`의 `prerelease-identifier: rc`는 release-drafter v6 기능입니다. 첫 실행 시 RC 번호 표기를 한 번 확인하세요(액션 버전에 따라 `rc.0`부터 시작할 수 있음).
