using UnityEngine;

/// <summary>
/// 시간 역행 - 전역
/// 이 카드 사용 시 현재 판의 상태를 저장합니다.
/// 8턴 후 이 상태로 돌아올지 결정할 수 있습니다.
/// </summary>
public class TimeReversalCard : CardData, ICard
{
    public void Execute(CardEffectArgs args = null)
    {
        // TODO: 현재 보드 상태를 스냅샷으로 저장
        //       8턴 후 플레이어에게 해당 상태로 복원할지 선택 UI 표시
    }
}
