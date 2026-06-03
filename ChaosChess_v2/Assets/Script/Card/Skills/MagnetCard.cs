using UnityEngine;
using System.Collections.Generic;
/// <summary>
/// 자석 - 타일 전용 (레어)
/// 선택한 타일에서 반경 1칸 내에 있는 랜덤한 기물 1기를 끌어옵니다.
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
    int[] dx = { -1, -1, -1, 0, 1, 1, 1, 0 };
    int[] dy = { -1, 0, 1, 1, 1, 0, -1, -1 };
    protected override void OnApply()
    {
        List<Piece> candidates = new List<Piece>();

        for (int i = 0; i < 8; i++)
        {
            int x = dx[i];
            int y = dy[i];
            Vector3Int candidate = new Vector3Int(TilePos.x + x, TilePos.y + y, TilePos.z);
            Piece p = BoardManager.Instance.GetPiece(candidate);
            if (p != null)
                candidates.Add(p);
        }

        if (candidates.Count > 0)
        {
            Piece selected = candidates[Random.Range(0, candidates.Count)];
            BoardManager.Instance.ForceTeleport(selected, TilePos);
        }

        Revert();
    }

    protected override void OnRevert()
    {
        Destroy(gameObject);
    }
}
