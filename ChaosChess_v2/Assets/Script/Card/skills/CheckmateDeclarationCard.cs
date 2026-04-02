using UnityEngine;

/// <summary>
/// 체크메이트 선언 - 전역
/// 다음 6턴 안에 체크를 당할 경우, 상대의 기물 5개가 자동으로 파괴됩니다.
/// </summary>
public class CheckmateDeclarationCard : CardData, ICard
{
    public void Execute(CardEffectArgs args = null)
    {
        // TODO: DataSO.LimitTurn(6)턴 동안 체크 감지 이벤트 등록
        //       체크 발생 시 상대 기물 5개 자동 파괴 처리
    }
}
