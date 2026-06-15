using UnityEngine;

/// <summary>카드를 사용할 수 없는 사유.</summary>
public enum CardBlockReason
{
    NoTargetPiece,        // 조건에 맞는 대상 기물이 없음
    AllPiecesAffected,    // 대상 기물에 이미 모든 효과가 적용됨
    NoSelectableTile,     // 카드를 놓을 빈 칸이 없음
    SelectionInProgress,  // 다른 카드 선택이 진행 중
    PlayerInCheck,        // 플레이어가 체크 상태
    NotPlayerTurn,        // 플레이어 턴이 아님
    ArenaInProgress,      // 투기장 진행 중
}

/// <summary>
/// 카드 사용 불가 상황을 콘솔 로그(개발용)와 화면 토스트(플레이어용)로 동시에 알립니다.
/// 호출부는 사유 enum만 넘기고, 메시지 문구는 이곳에서 일괄 관리합니다.
/// </summary>
public static class CardBlockNotifier
{
    public static void Notify(CardBlockReason reason, string cardName = null)
    {
        string msg = Format(reason, cardName);
        Debug.Log($"[카드 사용 불가] {msg}");
        IngameToastUI.Instance?.Show(msg);
    }

    /// <summary>손패 입력 차단 사유(체크/턴/투기장)를 현재 게임 상태로부터 결정합니다.</summary>
    public static CardBlockReason ResolveHandBlockReason()
    {
        GameManager gm = GameManager.Instance;
        if (gm == null) return CardBlockReason.NotPlayerTurn;
        if (gm.IsArenaMode) return CardBlockReason.ArenaInProgress;
        if (gm.IsPlayerInCheck) return CardBlockReason.PlayerInCheck;
        return CardBlockReason.NotPlayerTurn;
    }

    private static string Format(CardBlockReason reason, string cardName)
    {
        switch (reason)
        {
            case CardBlockReason.NoTargetPiece:
                return "조건에 맞는 대상 기물이 없습니다.";
            case CardBlockReason.AllPiecesAffected:
                return "대상 기물에 이미 모든 효과가 적용되어 있습니다.";
            case CardBlockReason.NoSelectableTile:
                return string.IsNullOrEmpty(cardName)
                    ? "카드를 놓을 수 있는 빈 칸이 없습니다."
                    : $"'{cardName}' 카드를 놓을 수 있는 빈 칸이 없습니다.";
            case CardBlockReason.SelectionInProgress:
                return "다른 카드를 사용하는 중입니다.";
            case CardBlockReason.PlayerInCheck:
                return "체크 상태에서는 카드를 사용할 수 없습니다.";
            case CardBlockReason.NotPlayerTurn:
                return "상대 턴에는 카드를 사용할 수 없습니다.";
            case CardBlockReason.ArenaInProgress:
                return "투기장 진행 중에는 카드를 사용할 수 없습니다.";
            default:
                return "카드를 사용할 수 없습니다.";
        }
    }
}
