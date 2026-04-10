using System.Collections.Generic;
using Unity.Android.Gradle.Manifest;
using UnityEngine;

/// <summary>
/// 거미줄 - 타일전용
/// 특정 칸에 거미줄을 설치합니다.
/// 킹을 제외한 기물이 거미줄에 걸리면 3턴동안 그 기물은 이동 할 수 없습니다.
/// </summary>

public class CobwebCard : CardData, ITileCard
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

        CobwebEffector effect = CreateGlobalEffector<CobwebEffector>();
        effect.TilePos = pos;
        effect.cobwebCard = this;
        effect.Apply();
    }

    public void OnStuckWap(Piece piece)
    {
        CobwebPieceEffector pieceEffect = piece.gameObject.AddComponent<CobwebPieceEffector>();
        pieceEffect.Init(piece, DataSO.LimitTurn);
        pieceEffect.Apply();
    }
}

public class CobwebEffector : GlobalEffector
{
    private bool isTriggered = false;

    public Vector3Int TilePos;
    public CobwebCard cobwebCard;

    protected override void OnApply()
    {
        BoardManager.Instance.RegisterGlobalEffector(this);
    }

    protected override void OnRevert()
    {
        BoardManager.Instance.UnregisterGlobalEffector(this);
        Destroy(gameObject);
    }

    public override bool CanPieceAct(Piece piece, Vector3Int from, Vector3Int to)
    {
        if (isTriggered) return true;
        if (piece is King) return true;

        Vector3Int dir = GetDir(from, to);
        Vector3Int cur = from;

        while (cur != to)
        {
            cur += dir;
            if (cur == TilePos)
            {
                isTriggered = true;
                BoardManager.Instance.ForceTeleport(piece, TilePos, '\0', false);
                cobwebCard.OnStuckWap(piece);
                BoardManager.Instance.RefreshMoves();
                Revert();
                return false;
            }
        }
        return true;
    }

    private static Vector3Int GetDir(Vector3Int from, Vector3Int to)
    {
        Vector3Int d = to - from;

        int dx = d.x;
        int dy = d.y;

        // 직선 / 대각선 / 킹 계열
        if (dx == 0 || dy == 0 || Mathf.Abs(dx) == Mathf.Abs(dy))
        {
            return new Vector3Int(
                Mathf.Clamp(dx, -1, 1),
                Mathf.Clamp(dy, -1, 1),
                0
            );
        }

        // 비직선 (나이트 계열 처리)
        if (dx != 0 && dy != 0 && dx != dy)
        {
            int adx = Mathf.Abs(dx);
            int ady = Mathf.Abs(dy);

            // 큰 축 우선 분해 (단순화된 "한 스텝 방향")
            if (adx > ady)
            {
                return new Vector3Int(
                    dx / adx * 2,
                    dy / ady * 1,
                    0
                );
            }
            else
            {
                return new Vector3Int(
                    dx / adx * 1,
                    dy / ady * 2,
                    0
                );
            }
        }

        return Vector3Int.zero;
    }
}

public class CobwebPieceEffector : PieceEffector
{
    protected override void OnApply()
    {
        target.MoveFenOverride = "a";
        BoardManager.Instance.RefreshMoves();
    }

    protected override void OnRevert()
    {
        target.MoveFenOverride = null;
        BoardManager.Instance.RefreshMoves();
        Destroy(this);
    }
}