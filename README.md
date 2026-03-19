# Chaos-Chess-v2

혼돈의 체스 v2 입니다.

스마일게이트 인디게임 창작 공모전 출품작입니다.

# 깃허브 규칙

- 새로운 기능이나 사항이 추가되면  `add:`를 붙인다.
- 추가된 사항이나 기능이 있으면 `feat:`를 붙인다.
- 수정되었거나 변경된 점이 있으면 `modify:`를 붙인다.
- 오류 수정이나 버그 픽스 시 `fix:`를 붙인다.
- 설명 칸에 자기 이슈 번호`(Ex. #123)`를 적는다.
- Git-Flow 브랜치 기법을 적극 활용한다.

| 커밋 타입   | 의미                        | 릴리즈 노트 섹션 |
| ----------- | --------------------------- | ---------------- |
| `feat:`     | 새로운 기능 추가            | ✨ 새로운 기능   |
| `modify:`   | 기능 수정                   | 🔄 기능 수정     |
| `refactor:` | 코드 리팩토링               | ♻️ 리팩토링      |
| `fix:`      | 버그 수정                   | 🐛 버그 수정     |
| `remove:`   | 삭제                        | 🗑️ 삭제          |
| `docs:`     | 문서 수정                   | 📝 문서          |


## 커밋할 때

- Summary에는 위 커밋 타입을 작성한다.
  - 예시: `feat: 새로운 기능 추가`
- Description에는 부가적인 설명 및 연관된 이슈 번호를 작성한다.
  - 예시: `#12 여기에 설명 적는데 필요 없으면 안적어도 됩니다.`

## PR 올릴 때

- Reviewers: 이 작업내용을 확인받을 사람을 설정한다.
- Assignees: 일반적으로 자기 자신을 할당한다.
- Labels: 작업내용의 적절한 종류의 라벨을 선택한다.


## Stockfish 사용 방법

| 메서드 | 매개변수 | 반환값 | 설명 |
|--------|---------|--------|------|
| `InitEngine(string variant = "chess")` | `variant` : 체스 변형 이름 (기본값: `"chess"`) | `void` | 엔진을 초기화합니다. 게임 시작 시 반드시 한 번 호출해야 합니다. |
| `SetPosition(string fen, string moves = "")` | `fen` : FEN 형식 포지션 문자열<br>`moves` : UCI 형식 이동 기록 (기본값: `""`) | `void` | 현재 보드 포지션을 설정합니다. |
| `GetBestMoveAsync(int depth, int moveTimeMs, Action<string> callback)` | `depth` : 탐색 깊이 (0이면 moveTimeMs 기준)<br>`moveTimeMs` : 최대 탐색 시간 (ms)<br>`callback` : 탐색 완료 시 호출되는 콜백 | `void` | 비동기로 최선의 수를 탐색합니다. AI 차례에 호출합니다. 콜백은 Unity 메인 스레드에서 실행됩니다. |
| `GetLegalMoves()` | 없음 | `string[]` : UCI 형식 이동 목록<br>예) `["e2e3", "e2e4", ...]` | 현재 포지션에서 모든 합법적인 수를 반환합니다. |
| `GetLegalMovesFromSquare(string square)` | `square` : UCI 형식 칸 좌표<br>예) `"e2"` | `string[]` : 해당 칸에서 출발하는 UCI 이동 목록<br>예) `["e2e3", "e2e4"]` | 특정 칸에서 이동 가능한 수를 반환합니다. |
| `GetGameResult()` | 없음 | `1` : 진행 중<br>`0` : 스테일메이트<br>`-1` : 체크메이트 | 현재 게임 결과를 반환합니다. |

## 필수 설정

- 씬에는 반드시 `FairyStockfishBridge`와 `UnityMainThreadDispatcher`가 있어야 합니다.

## 기타 사항
- 현재 PC 테스트 버전은 **매우 불안정한 상태**입니다. 순수 테스트 용도로만 사용해주시길 바랍니다.
- `ChessGameManager` 스크립트를 AI를 사용하여 제작하였습니다. 필요하다면 참고 및 응용해도 좋습니다.

