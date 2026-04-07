using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 가호 - 타일 전용 (고급)
/// 선택한 칸에 기물이 2턴 동안 있을 경우 다음 등급으로 승격됩니다.
/// 중간에 기물이 빠지면 타일 효과가 사라집니다.
/// </summary>
public class BlessingCard : CardData, ITileCard
{
    private TileSelector selector;

    private void Awake()
    {
        selector = FindFirstObjectByType<TileSelector>();
    }

    public void LoadTileSelector()
    {
        if (selector == null) selector = FindFirstObjectByType<TileSelector>();
        selector.EnableSelector(this);
    }

    public void Execute(CardEffectArgs args = null)
    {
        Vector3Int pos = args.TargetPos[0];

        GameObject obj = new GameObject("BlessingEffect");
        BlessingEffect effect = obj.AddComponent<BlessingEffect>();

        effect.Init(pos);
        effect.Apply();
        effect.duration = DataSO.MaintainTurn;
    }
}

public class BlessingEffect : TileEffector
{
    public int duration;

    private Piece currentPiece;
    private bool isScheduled = false;

    protected override void OnApply()
    {
        BoardManager.Instance.RegisterTileEffector(tilePos, this);
    }

    protected override void OnRevert()
    {
        BoardManager.Instance.UnregisterTileEffector(tilePos, this);
        Destroy(gameObject);
    }

    public override void OnPieceEnter(Piece piece)
    {
        SchedulePromotion(piece);
    }

    private void SchedulePromotion(Piece piece)
    {
        currentPiece = piece;

        if (isScheduled) return;
        isScheduled = true;

        GameManager.Instance.AppendAction(duration, () =>
        {
            if (this == null) return; // GameObject 파괴됨

            if (currentPiece != null && currentPiece == piece)
            {
                Promote(currentPiece);

                isScheduled = false;
                SchedulePromotion(BoardManager.Instance.GetPiece(piece.Pos));
            }
            else
            {
                Revert();
            }
        });
    }

    public override void OnPieceExit(Piece piece)
    {
        if (piece == currentPiece)
        {
            currentPiece = null;
            Revert();
        }
    }

    private void Promote(Piece piece)
    {
        char change = ' ';

        switch (piece.Type)
        {
            case PieceType.Pawn:
                change = 'n';
                break;
            case PieceType.Knight:
                change = 'r';
                break;
            case PieceType.Bishop:
                change = 'r';
                break;
            case PieceType.Rook:
                change = 'q';
                break;
        }
        if (change == ' ')
        {
            Revert();
            return;
        }

        BoardManager.Instance.ChangePiece(piece.Pos, piece.Color, change);
    }
}