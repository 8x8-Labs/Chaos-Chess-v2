using UnityEngine;

/// <summary>
/// 배수진 - 전역
/// 이 카드 사용 시 자신과 상대 모두 3턴 동안 기물을 전진시키는 방향으로만 이동할 수 있습니다.
/// </summary>
public class StagFightCard : CardData, ICard
{
    public void Execute(CardEffectArgs args = null)
    {
        // TODO: 자신과 상대 모두 3턴 동안 전진 방향으로만 이동 가능하도록 제한 처리
    }
}
