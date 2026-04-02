using UnityEngine;

/// <summary>
/// 미인계 - 전역
/// 카드 사용 시 상대 킹을 퀸과의 맨해튼 거리가 줄어드는 방향으로 이동시킵니다.
/// </summary>
public class HoneytrapCard : CardData, ICard
{
    public void Execute(CardEffectArgs args = null)
    {
        // TODO: 상대 킹의 위치와 아군 퀸의 위치를 구한 뒤
        //       맨해튼 거리가 줄어드는 방향(가장 가까워지는 방향)으로 상대 킹 1칸 강제 이동 처리
    }
}
