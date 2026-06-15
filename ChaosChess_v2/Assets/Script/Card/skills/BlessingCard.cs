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

        effect.CardSO = DataSO;
        effect.DataSO = DataSO;
        
        effect.Init(pos);
        effect.Apply();
        effect.duration = DataSO.MaintainTurn;
    }
}

public class BlessingEffect : TileEffector
{
    public CardDataSO DataSO;

    public int duration;

    private Piece currentPiece;
    private bool isScheduled = false;

    protected override void OnApply()
    {
        Piece.OnPieceDestroyed += HandlePieceDestroyed;

        ShowTileEffect(DataSO);

        BoardManager.Instance.RegisterTileEffector(tilePos, this);
    }

    protected override void OnRevert()
    {
        Piece.OnPieceDestroyed -= HandlePieceDestroyed;

        ClearTileEffect();

        BoardManager.Instance.UnregisterTileEffector(tilePos, this);
        Destroy(gameObject);
    }

    private void HandlePieceDestroyed(Piece piece)
    {
        if (piece == currentPiece) { currentPiece = null; Revert(); }
    }

    // Revert()를 거치지 않고 오브젝트가 파괴되는 예외적 경로 대비
    protected override void OnDestroy()
    {
        base.OnDestroy();
        Piece.OnPieceDestroyed -= HandlePieceDestroyed;
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
                SchedulePromotion(BoardManager.Instance.GetPiece(tilePos));
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

        // ChangePiece가 기존 기물을 파괴하므로 좌표/색을 먼저 캡처한다.
        Vector3Int pos = piece.Pos;
        PieceColor color = piece.Color;

        BoardManager.Instance.ChangePiece(pos, color, change);

        PlayPromoteVFX(pos);
    }

    // 승격은 별도 Effector를 만들지 않으므로 SO의 기물 부여 연출(PieceEffectVFX)을 직접 재생한다.
    private void PlayPromoteVFX(Vector3Int pos)
    {
        CardVFXConfig vfx = DataSO != null ? DataSO.PieceEffectVFX : null;
        if (vfx == null) return;

        Piece promoted = BoardManager.Instance.GetPiece(pos);
        Vector3 worldPos = promoted != null
            ? promoted.transform.position
            : BoardManager.Instance.GridPosToWorldPos(pos);

        VFXSpawner.SpawnOneShot(vfx.ApplyVFXPrefab, worldPos,
            promoted != null ? promoted.transform : null);
        if (vfx.PlayApplyAnim && promoted != null)
            VFXSpawner.PlayPunch(promoted.transform, vfx.AnimStrength, vfx.AnimDuration);
    }
}
