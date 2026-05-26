---
name: auto-commit
description: 현재 변경사항을 기능 단위로 자동 분할하여 커밋하는 스킬. 사용자가 "자동 커밋", "커밋 올려줘", "변경사항 커밋", "커밋 분할", "staged 커밋" 등을 언급하면 반드시 이 스킬을 사용한다. 변경된 파일이 여러 개이거나 기능 단위로 나눠서 커밋하고 싶을 때 항상 트리거된다.
---
 
 ## 언어 규칙 (필수)
 
- **커밋 메시지 type·scope**: 영어로 작성 (`feat`, `fix`, `Card` 등)
- **커밋 메시지 subject (설명)**: 반드시 **한글**로 작성
- 예시: `feat(Card): 카드 뒤집기 애니메이션 추가`

---

# Auto Commit
 
변경사항을 AI가 기능 단위로 분석하여 의미 있는 커밋으로 자동 분할·생성하는 스킬.  
커밋 메시지는 **Conventional Commits** 규칙을 따르며, **Unity 프로젝트** 환경을 기준으로 동작한다.

 
---
 
## Unity 프로젝트 특이사항
 
**자동 제외 파일 (커밋 그룹에서 분리):**
- `*.meta` — 대응되는 원본 파일과 **항상 같은 그룹**으로 묶는다 (절대 단독 커밋 금지)
- `Library/`, `Temp/`, `obj/` — 커밋 대상에서 제외 (`.gitignore`에 있어야 정상)
- `ProjectSettings/` — 의도적 변경이면 `chore(Build)` 그룹으로 별도 분리
 
**Unity 도메인 scope 기준:**
 
| 변경 영역 | scope |
|-----------|-------|
| 씬(Scene) 파일 | `Scene` |
| UI / uGUI 스크립트·프리팹 | `UI` |
| 스크립터블 오브젝트 | `SO` |
| 카드 관련 | `Card` |
| 게임 매니저 | `Manager` |
| 애니메이션 컨트롤러·클립 | `Anim` |
| 셰이더·머티리얼 | `Shader` |
| 빌드·에디터 설정 | `Build` |
| 외부 패키지·의존성 | `Package` |
 
---
 
## 워크플로우
 
### 1단계: 변경사항 수집
 
```bash
# unstaged 변경사항
git diff
 
# staged 변경사항
git diff --staged
 
# untracked 파일 포함 전체 상태
git status
```
 
### 2단계: 기능 단위로 그룹 분류
 
수집한 diff를 분석해 **논리적으로 독립된 기능 단위**로 파일을 묶는다.
 
**분류 기준:**
- 같은 Unity 도메인/기능에 속하는 파일끼리 묶음 (예: Card 관련 스크립트+프리팹, UI 씬+스크립트)
- `.meta` 파일은 반드시 대응 원본 파일과 같은 그룹에 포함
- 테스트 코드는 대응되는 구현 코드와 같은 그룹으로 묶음
- `ProjectSettings/` 변경은 별도 `chore(Build)` 그룹으로 분리
- 씬(`.unity`) 파일은 해당 씬과 관련된 스크립트와 같은 그룹 또는 별도 `Scene` 그룹
 
**그룹 예시:**
```
그룹 1: 카드 뒤집기 기능
  - Assets/Scripts/Card/CardFlip.cs
  - Assets/Scripts/Card/CardFlip.cs.meta
  - Assets/Prefabs/Card/CardFlip.prefab
  - Assets/Prefabs/Card/CardFlip.prefab.meta
 
그룹 2: 인벤토리 UI 수정
  - Assets/Scripts/UI/InventoryPanel.cs
  - Assets/Scripts/UI/InventoryPanel.cs.meta
 
그룹 3: 빌드 설정 변경
  - ProjectSettings/ProjectSettings.asset
```
 
### 3단계: 커밋 메시지 생성
 
각 그룹에 대해 **Conventional Commits** 형식으로 메시지를 작성한다.
 
**형식:**
```
<type>(<scope>): <subject>
```
 
**type 선택 기준:**
 
| type | 사용 상황 |
|------|-----------|
| `feat` | 새로운 기능 추가 |
| `fix` | 버그 수정 |
| `refactor` | 기능 변화 없는 코드 개선 |
| `style` | 코드 포맷, 공백 정리 (기능 변화 없음) |
| `test` | 테스트 코드 추가/수정 |
| `chore` | 빌드, 설정, 패키지 변경 |
| `docs` | 문서 변경 |
| `perf` | 성능 개선 |
 
**scope:** Unity 도메인 기준 (위 표 참고). type·scope는 반드시 **영어**로 작성  
**subject:** 50자 이내, 반드시 **한글**로 작성
 
**작성 예시:**
```
feat(Card): 카드 뒤집기 애니메이션 추가
fix(UI): 인벤토리 슬롯 정렬 오류 수정
chore(Build): ProjectSettings 빌드 타겟 변경
refactor(Manager): 게임 매니저 싱글톤 구조 개선
```
 
### 4단계: 커밋 계획 확인
 
사용자에게 아래 형식으로 전체 계획을 보여주고 **반드시 확인**받는다.
 
```
총 N개의 커밋을 생성합니다.
 
[1/N] feat(card): 카드 등록 폼 컴포넌트 추가
  - src/components/CardForm.tsx
  - src/api/card.ts
  - src/components/CardForm.test.tsx
 
[2/N] chore(config): ESLint 규칙 업데이트
  - .eslintrc.js
 
진행할까요?
```
 
> **사용자 승인 전에는 절대 커밋하지 않는다.**
 
### 5단계: 순차 커밋 실행
 
승인 후 그룹 순서대로 커밋을 실행한다.
 
```bash
# 해당 그룹 파일만 stage
git add <file1> <file2> ...
 
# 커밋
git commit -m "<type>(<scope>): <subject>"
```
 
각 커밋 후 성공 여부를 확인하고 다음 그룹으로 진행한다.  
실패 시 즉시 멈추고 사용자에게 오류 내용을 알린다.
 
### 6단계: 결과 보고
 
모든 커밋 완료 후 요약을 출력한다.
 
```
✅ 커밋 완료 (N개)
 
abc1234  feat(card): 카드 등록 폼 컴포넌트 추가
def5678  chore(config): ESLint 규칙 업데이트
 
push할까요?
```
 
사용자가 원하면 `git push`까지 이어서 실행한다.
 
---
 
## 에러 처리
 
| 상황 | 대응 |
|------|------|
| 변경사항 없음 | "커밋할 변경사항이 없습니다" 안내 |
| 병합 충돌 있음 | 충돌 파일 목록 알리고 중단 |
| 커밋 중 실패 | 실패한 그룹 알리고 중단, 나머지는 수동 처리 안내 |
| 훅(pre-commit) 실패 | 훅 오류 메시지 그대로 출력 |
 
---
 
## 전제 조건
 
- Git 저장소 초기화 완료
- 커밋할 변경사항 존재 (staged 또는 unstaged)
 