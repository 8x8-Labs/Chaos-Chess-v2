using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 정신 집중 - 기물 전용 (전설, 아마존)
/// 선택 기물은 일정 수치 동안 상호작용할 수 없고, 이후 아마존으로 승격됩니다.
/// </summary>
public class ConcentrationCard : CardData, IPieceCard
{
    private PieceSelector selector;
    Vector3Int pos;
    public Piece wallPrefab;
    PieceColor color;
    CardEffectArgs card = new CardEffectArgs();

    private void Awake()
    {
        selector = FindFirstObjectByType<PieceSelector>();
        card.LimitTurn = 4;
    }

    public void LoadPieceSelector()
    {
        if (selector == null) selector = FindFirstObjectByType<PieceSelector>();
        selector.EnableSelector(this);
    }

    public void Execute(CardEffectArgs args = null)
    {
        foreach (Piece piece in args.Targets)
        {
            Vector3Int targetPos = piece.Pos;
            pos = targetPos;
            PieceColor pieceColor = piece.Color;
            color = pieceColor;

            //Piece wall = Instantiate(wallPrefab, BoardManager.Instance.PieceSpawnTransform);
            //wall.Init(targetPos, pieceColor, piece.WhitePieceSprite, piece.BlackPieceSprite);

            // 위치 강제 동기화 (필요 시)
            // wall.transform.position = BoardManager.Instance.GridToWorld(targetPos);

            BoardManager.Instance.CreatePromotionPiece(targetPos, pieceColor, 'a');
            GameManager.Instance.AppendAction(card.LimitTurn, () => Change(targetPos, pieceColor));
            
        }
    }

    private void Change(Vector3Int pos, PieceColor color)
    {
        Debug.Log("callbacked");
        //Amazon amazon = new Amazon();
        //amazon.Init(pos, color);
        BoardManager.Instance.CreatePromotionPiece(pos, color, 's');
    }
}
