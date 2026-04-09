using UnityEngine;
using System.Collections.Generic;
using System.Security.Cryptography;
/// <summary>
/// 자석 - 타일 전용 (레어)
/// 선택한 타일에서 죽은 기물 중 가장 가치가 높은 기물이 부활합니다
/// </summary>
public class MagnetCard : CardData, ITileCard
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
        MagnetEffector effector = CreateTileEffector<MagnetEffector>(args.TargetPos[0]);
        effector.Apply();
    }
}

public class MagnetEffector : TileEffector
{
    protected override void OnApply()
    {
        int[] dx = { -1, -1, -1, 0, 1, 1, 1, 0 };
        int[] dy = { -1, 0, 1, 1, 1, 0, -1, -1 };
        for (int i = 0; i < 8; i++)
        {
            int x = dx[i];
            int y = dy[i];
            Vector3Int candidate = new Vector3Int(TilePos.x + x, TilePos.y + y, TilePos.z);
            Piece p = BoardManager.Instance.GetPiece(candidate);
            if (p != null)
            {
                BoardManager.Instance.ForceTeleport(p, TilePos);
                Revert();
                return;
            }

        }
        Revert();
    }
    protected override void OnRevert()
    {
        Destroy(gameObject);
    }
}