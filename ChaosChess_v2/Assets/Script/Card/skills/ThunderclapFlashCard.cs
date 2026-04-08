

using JetBrains.Annotations;
using UnityEngine;

/// <summary>
/// 벽력일섬 - 기물 전용 (고급)
/// 선택한 룩이 한 턴 동안 지나간 자리의 모든 기물(본인기물 포함)이 죽습니다. ← 4칸 수 제한
/// </summary>
public class ThunderclapFlashCard : CardData, IPieceCard
{
    private PieceSelector selector;

    private void Awake()
    {
        selector = FindFirstObjectByType<PieceSelector>();
    }

    public void LoadPieceSelector()
    {
        if (selector == null) selector = FindFirstObjectByType<PieceSelector>();
        selector.EnableSelector(this);
    }

    public void Execute(CardEffectArgs args = null)
    {
        Piece piece = args.Targets[0];
        ThunderclapFlashEffector effector = CreatePieceEffector<ThunderclapFlashEffector>(piece);

        effector.Apply();
        effector.targetPos = piece.Pos;
    }
}

public class ThunderclapFlashEffector : PieceEffector
{
    public Vector3Int targetPos;
    protected override void OnApply()
    {
        target.MoveFenOverride = "m";
        BoardManager.Instance.RefreshMoves();
    }

    protected override void OnRevert()
    {

        Vector3Int start = targetPos;
        Vector3Int end = target.Pos;

        int dx = end.x - start.x;
        int dy = end.y - start.y;

        int steps = Mathf.Max(Mathf.Abs(dx), Mathf.Abs(dy));

        int stepX = dx == 0 ? 0 : dx / Mathf.Abs(dx);
        int stepY = dy == 0 ? 0 : dy / Mathf.Abs(dy);

        for (int i = 0; i < steps; i++)
        {
            Vector3Int pos = new Vector3Int(start.x + stepX * i, start.y + stepY * i, 0);
            BoardManager.Instance.DestroyPiece(pos);
        }

        target.MoveFenOverride = null;
        BoardManager.Instance.RefreshMoves();

        Destroy(this);
    }
    public override void OnTurnChanged()
    {
        Revert();
    }
}
