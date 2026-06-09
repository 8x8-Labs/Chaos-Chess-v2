---
name: auto-pr
description: GitHub PR을 자동으로 생성하는 스킬. 사용자가 "PR 만들어줘", "PR 올려줘", "pull request 생성", "변경사항 PR", "브랜치 PR" 등을 언급하면 반드시 이 스킬을 사용한다. Git 저장소가 있는 상태에서 PR 관련 작업이 필요할 때 항상 트리거된다.
---
 
# Auto PR
 
Git 변경사항을 자동으로 감지하고 GitHub PR을 생성하는 스킬.  
**Unity 프로젝트** 환경을 기준으로 동작한다.
 
---
 
## 언어 규칙 (필수)
 
- **PR 제목 태그**: 변경 유형에 맞는 영어 태그(`[Feat]`, `[Fix]` 등)를 앞에 붙인다 — 이 태그로 release-drafter autolabeler가 **버전 라벨을 자동 부여**한다 (아래 "버전 / 릴리스 규칙" 참고)
- **PR 본문 전체**: 반드시 한글로 작성
- 제목 예시: `[Feat] 카드 등록 UI 추가`, `[Fix] 인벤토리 슬롯 정렬 오류 수정`
 
---
 
## Unity 프로젝트 특이사항
 
**무시할 파일 (PR 설명에서 제외):**
- `*.meta` — Unity 자동 생성 메타파일, 변경사항으로 언급하지 않음
- `ProjectSettings/` — 의도적 변경이 아니면 별도 언급 생략
- `Packages/packages-lock.json` — 패키지 자동 갱신
 
**주요 Unity 도메인 scope 예시:**
 
| 변경 영역 | scope 예시 |
|-----------|-----------|
| 씬(Scene) 작업 | `Scene` |
| UI / uGUI | `UI` |
| 스크립터블 오브젝트 | `SO` |
| 카드 관련 | `Card` |
| 게임 매니저 | `Manager` |
| 애니메이션 | `Anim` |
| 셰이더 | `Shader` |
| 빌드/에디터 설정 | `Build` |
 
---
 
## 워크플로우
 
### 1단계: 저장소 상태 파악
 
```bash
# 현재 브랜치 확인
git branch --show-current
 
# 변경사항 확인
git status
git diff --stat
 
# 최근 커밋 로그 (base 브랜치 대비)
git log origin/main..HEAD --oneline
```
 
### 2단계: 변경사항 분석
 
수집한 정보를 바탕으로 다음을 파악한다:
- **base 브랜치**: 보통 `main` 또는 `develop`
- **head 브랜치**: 현재 작업 브랜치
- **변경된 파일 목록**: 어떤 파일이 추가/수정/삭제됐는지
- **커밋 메시지들**: 변경의 의도 파악
 
### 3단계: PR 제목 및 본문 생성
 
변경사항을 분석해 자동으로 작성한다.
 
**PR 제목 규칙:**
- `[태그] <한글 설명>` 형식 사용. 설명은 **반드시 한글**
- **태그는 변경 유형에 맞게 고른다.** release-drafter autolabeler가 제목 태그(또는 브랜치명)를 보고 버전 라벨을 자동 부여하므로, 태그가 틀리면 릴리스 노트 분류와 버전 증가(minor/patch)가 어긋난다.

| 변경 유형 | 제목 태그 | 부여되는 라벨 | 버전 |
|-----------|-----------|---------------|------|
| 새 기능 | `[Feat]` (또는 `[Feature]`) | `feat` | minor |
| 버그 수정 | `[Fix]` (또는 `[Bug]`) | `fix` | patch |
| 기능 수정 | `[Modify]` | `modify` | patch |
| 리팩토링 | `[Refactor]` | `refactor` | patch |
| 삭제 | `[Remove]` | `remove` | patch |
| 문서 | `[Docs]` | `docs` | patch |
| 빌드·설정·패키지 | `[Chore]` | `chore` | patch |
| 호환성 깨짐 | (제목 태그 없음 — `major` 라벨 수동 부여) | `major` | major |

- 예: `[Feat] 카드 뒤집기 애니메이션 추가`, `[Fix] 인벤토리 슬롯 정렬 오류 수정`
- 브랜치명도 autolabeler 대상이다: `feature/*`→`feat`, `fix/*`·`bugfix/*`→`fix`, `refactor/*`→`refactor`, `chore/*`→`chore`. 제목 태그와 브랜치 유형이 일치하도록 맞춘다.
 
**PR 본문 규칙:**
- 본문 전체를 **한글**로 작성한다
- `.meta` 파일 변경은 본문에서 언급하지 않는다
 
**PR 본문 템플릿:**
```markdown
# 개요
<!-- 이 PR의 목적과 배경을 간략히 설명 -->
 
# 내용
<!-- 변경 내용을 소제목으로 나눠서 설명 -->
## <변경 영역 제목>
<!-- 해당 영역의 변경사항 설명 -->
 
## <변경 영역 제목>
<!-- 해당 영역의 변경사항 설명 -->
 
# 기타 사항
<!-- 리뷰어가 알아야 할 추가 정보, 스크린샷, 참고 링크 등. 없으면 "없음" -->
```
 
### 4단계: 라벨 자동 결정
 
변경사항을 분석해 아래 기준으로 라벨을 결정한다.
 
> ⚠️ **라벨이 곧 버전이다.** 이 저장소는 [release-drafter](https://github.com/release-drafter/release-drafter)가 **PR 라벨**로 릴리스 노트 분류와 다음 SemVer 버전을 계산한다. autolabeler가 제목/브랜치로 1차 부여하지만 자동 라벨이 비거나 틀릴 수 있으니, 아래 표의 **release-drafter 라벨(`feat`/`fix`/`modify`/…)** 을 직접 확인·부여한다. 상세 규칙은 [docs/RELEASE.md](../../../docs/RELEASE.md)와 `.github/release-drafter.yml` 참고.
 
**기본 라벨 규칙 (release-drafter 버전 매핑):**
 
| 조건 | 라벨 | 버전 영향 |
|------|------|-----------|
| 새 기능 추가 (신규 API/컴포넌트 등) | `feat` | **minor** |
| 버그 수정 (오류 해결 등) | `fix` | patch |
| 기존 기능 수정 (동작 변경) | `modify` | patch |
| 리팩토링 (동작 변화 없는 개선) | `refactor` | patch |
| 코드·파일 삭제 | `remove` | patch |
| 문서 수정 (README, 주석 등) | `docs` | patch |
| 빌드·설정·의존성·패키지·테스트·스타일·성능 | `chore` | patch |
| 호환성 깨지는 변경 | `major` | **major** |
| 릴리스 노트에서 제외 | `skip-changelog` | 없음 |
 
- `feat` 라벨이 하나라도 있으면 그 PR은 **minor** 로 올라간다(유형이 섞이면 가장 높은 버전 영향을 따른다). 단순 수정 PR에 `feat`를 잘못 붙이지 않도록 주의한다.
- 위 라벨이 저장소에 없으면 [docs/RELEASE.md](../../../docs/RELEASE.md) 5절의 셋업 명령으로 생성한다.
 
**Unity 특화 라벨 규칙:**
 
| 조건 | 라벨 |
|------|------|
| `.unity` 씬 파일 변경 | `scene` |
| 셰이더·머티리얼 변경 | `shader` |
| 애니메이션 컨트롤러·클립 변경 | `animation` |
| `ProjectSettings/` 변경 | `build` |
| 외부 패키지(`Packages/`) 변경 | `chore` |

> Unity 특화 라벨은 **보조 분류용**이다. 버전 계산에는 영향을 주지 않으므로, 위 "기본 라벨 규칙"의 release-drafter 버전 라벨(`feat`/`fix`/… )을 **반드시 함께** 붙인다.
 
**카드 관련 라벨 규칙 (필수):**
- 변경된 파일 경로, 커밋 메시지, PR 내용 중 다음 키워드가 하나라도 포함되면 `Card` 라벨을 **반드시** 추가한다:
  - `card`, `카드`, `Card`, `payment`, `결제`, `카드사`, `신용카드`, `체크카드`
 
> 라벨은 여러 개 적용 가능하다. `Card` 라벨은 다른 라벨과 함께 병기한다.
 
**라벨 적용 예시:**
- 카드 결제 버그 수정 → `fix`, `Card`
- 카드 컴포넌트 신규 구현 → `feat`, `Card`
- 카드 관련 없는 리팩토링 → `refactor`
 
### 5단계: PR 생성 전 확인
 
사용자에게 다음을 보여주고 확인받는다:
- Base 브랜치 → Head 브랜치
- PR 제목
- PR 본문 (요약)
- 적용될 라벨 목록
 
> **반드시 사용자 확인 후 PR을 생성한다.**
 
### 6단계: PR 생성
 
`gh` CLI 또는 GitHub API를 사용한다.
 
**gh CLI 사용 시 (권장):**
```bash
gh pr create \
  --base <base-branch> \
  --head <head-branch> \
  --title "<PR 제목>" \
  --body "<PR 본문>" \
  --label "<label1>" \
  --label "<label2>"
```
 
> `--label` 옵션은 라벨이 GitHub 저장소에 미리 생성되어 있어야 적용된다. 라벨이 없으면 생성 후 적용하거나 사용자에게 안내한다.
 
**gh CLI 미설치 시 — GitHub API 사용:**
```bash
# 1. PR 생성
PR_URL=$(curl -s -X POST \
  -H "Authorization: token $GITHUB_TOKEN" \
  -H "Accept: application/vnd.github.v3+json" \
  https://api.github.com/repos/<owner>/<repo>/pulls \
  -d '{
    "title": "<PR 제목>",
    "body": "<PR 본문>",
    "head": "<head-branch>",
    "base": "<base-branch>"
  }' | jq -r '.number')
 
# 2. 라벨 추가
curl -X POST \
  -H "Authorization: token $GITHUB_TOKEN" \
  -H "Accept: application/vnd.github.v3+json" \
  https://api.github.com/repos/<owner>/<repo>/issues/$PR_URL/labels \
  -d '{"labels": ["<label1>", "<label2>"]}'
```
 
### 7단계: 결과 보고
 
PR 생성 후 URL과 적용된 라벨 목록을 사용자에게 알려준다.
 
---
 
## 에러 처리
 
| 상황 | 대응 |
|------|------|
| 커밋되지 않은 변경사항 있음 | 커밋 여부를 사용자에게 묻기 |
| 원격 브랜치 없음 | `git push -u origin <branch>` 먼저 실행 |
| 이미 PR 존재 | 기존 PR URL 안내 |
| 인증 실패 | `gh auth login` 또는 `GITHUB_TOKEN` 설정 안내 |
| base와 head가 동일 | 브랜치 확인 요청 |
 
---
 
## 전제 조건
 
- Git 저장소 초기화 완료
- `gh` CLI 설치 및 인증 완료 **또는** `GITHUB_TOKEN` 환경변수 설정
- 원격 저장소(GitHub)에 브랜치가 push된 상태
 
---
 
## 참고
 
- `gh` CLI 설치: https://cli.github.com
- GitHub Token 생성: Settings → Developer settings → Personal access tokens
 