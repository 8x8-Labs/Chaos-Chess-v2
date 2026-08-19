using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 상대가 사용한 카드를 이쪽 보드에 그대로 적용합니다.
///
/// 선택 UI(PieceSelector/TileSelector)는 카드를 쓰는 쪽의 로컬 UI이므로 여기서는 열지 않고,
/// 메시지에 담겨 온 대상을 복원해 ICard.Execute를 직접 호출합니다.
/// AI가 카드를 쓸 때와 같은 경로입니다.
/// </summary>
public static class RemoteCardExecutor
{
    /// <summary>
    /// 상대 카드를 적용하기 직전에 발행됩니다.
    /// 카드 연출을 더 크게 보여주고 싶은 UI가 여기에 붙으면 됩니다.
    /// </summary>
    public static event Action<CardDataSO> CardApplying;

    public static bool TryExecute(MatchMessage message, PieceColor casterColor)
    {
        if (message == null || string.IsNullOrEmpty(message.CardId))
            return false;

        CardData cardData = FindCardById(message.CardId);
        if (cardData == null || cardData.DataSO == null)
        {
            Debug.LogError($"[Remote] 카드를 찾지 못했습니다: '{message.CardId}'. 카드 DB가 서로 다를 수 있습니다.");
            return false;
        }

        ICard card = cardData.GetComponent<ICard>();
        if (card == null)
        {
            Debug.LogError($"[Remote] '{message.CardId}'에 ICard 구현이 없습니다.");
            return false;
        }

        CardDataSO dataSO = cardData.DataSO;
        if (!TryBuildArgs(dataSO, message.Targets, casterColor, out CardEffectArgs args))
            return false;

        // 무엇 때문에 판이 바뀌었는지 알려주지 않으면 기물이 제멋대로 움직이는 것처럼 보입니다.
        // 효과를 적용하기 전에 먼저 알립니다.
        AnnounceToPlayer(dataSO);

        // CardRandomizerManager가 없는 환경(카드 이펙트 랩 등)에서는 직접 실행합니다.
        if (CardRandomizerManager.Instance != null)
            CardRandomizerManager.Instance.ExecuteCard(dataSO, () => card.Execute(args));
        else
            card.Execute(args);

        BoardManager.Instance?.RefreshMoves();
        Debug.Log($"[Remote] 상대 카드 적용: '{dataSO.CardName}' ({message.CardId})");
        return true;
    }

    /// <summary>
    /// 상대가 어떤 카드를 썼는지 화면에 알립니다.
    ///
    /// 토스트는 CardBlockNotifier와 같은 방식으로 직접 부릅니다.
    /// 더 큰 연출이 필요하면 CardApplying에 UI를 붙이면 됩니다.
    /// </summary>
    private static void AnnounceToPlayer(CardDataSO dataSO)
    {
        string cardName = string.IsNullOrWhiteSpace(dataSO.CardName)
            ? dataSO.name
            : dataSO.CardName;

        IngameToastUI.Instance?.Show($"상대가 {cardName} 사용");
        CardApplying?.Invoke(dataSO);
    }

    /// <summary>AiCardId로 카드 프리팹의 CardData를 찾습니다. 인스턴스화하지 않고 프리팹 컴포넌트를 그대로 씁니다.</summary>
    private static CardData FindCardById(string cardId)
    {
        CardRandomizerManager manager = CardRandomizerManager.Instance;
        if (manager == null || manager.AllCards == null)
            return null;

        foreach (GameObject cardObject in manager.AllCards)
        {
            if (cardObject == null) continue;

            CardData cardData = cardObject.GetComponent<CardData>();
            if (cardData == null || cardData.DataSO == null) continue;

            if (cardData.DataSO.AiCardId == cardId)
                return cardData;
        }

        return null;
    }

    /// <summary>메시지의 UCI 좌표들을 카드 종류에 맞는 실행 인자로 되돌립니다.</summary>
    private static bool TryBuildArgs(
        CardDataSO dataSO,
        string[] targets,
        PieceColor casterColor,
        out CardEffectArgs args)
    {
        args = new CardEffectArgs
        {
            HasCasterColor = true,
            CasterColor = casterColor,

            // 선택 UI를 여는 대신 좌표만으로 실행합니다. 여러 단계로 대상을 고르는 카드가
            // 이 값을 보고 UI를 건너뜁니다.
            TargetsPreselected = true
        };

        BoardManager boardManager = BoardManager.Instance;
        if (boardManager == null)
            return false;

        switch (dataSO.Type)
        {
            case CardType.Piece:
                args.LimitTurn = dataSO.PieceLimitTurn;
                args.Targets = new List<Piece>();

                if (targets == null)
                    break;

                // 텔레포트처럼 기물에 이어 타일까지 고르는 카드는 앞쪽 RequiredPieceCount개가
                // 기물 좌표이고 나머지가 타일 좌표입니다. 송신 측이 그 순서로 이어 붙입니다.
                int pieceCount = dataSO.TileCount > 0
                    ? Mathf.Min(dataSO.RequiredPieceCount, targets.Length)
                    : targets.Length;

                for (int i = 0; i < targets.Length; i++)
                {
                    Vector3Int pos = boardManager.UCIToGrid(targets[i]);

                    if (i >= pieceCount)
                    {
                        args.TargetPos ??= new List<Vector3Int>();
                        args.TargetPos.Add(pos);
                        continue;
                    }

                    Piece piece = boardManager.GetPiece(pos);

                    // 대상 기물이 없다면 양쪽 보드가 어긋난 것이므로 적용하지 않습니다.
                    if (piece == null)
                    {
                        Debug.LogError($"[Remote] 대상 칸 {targets[i]}에 기물이 없습니다. 보드 상태가 어긋났습니다.");
                        return false;
                    }

                    args.Targets.Add(piece);
                }
                break;

            case CardType.Tile:
                args.LimitTurn = dataSO.MaintainTurn;
                args.TargetPos = new List<Vector3Int>();

                if (targets == null)
                    break;

                foreach (string square in targets)
                {
                    args.TargetPos.Add(boardManager.UCIToGrid(square));
                }
                break;

            case CardType.Global:
                args.LimitTurn = dataSO.HasLimit ? dataSO.LimitTurn : -1;
                break;
        }

        return true;
    }
}
