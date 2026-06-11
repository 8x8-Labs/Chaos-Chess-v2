using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 성 위의 말 - 기물 전용 (챈슬러 합체)
/// 턴 소모 없이 나이트를 선택함
/// 나이트가 가장 가까운 룩 자리로 이동해 합쳐져 챈슬러로 승격 (나이트가 있던 자리에는 기물이 남지 않음).
/// </summary>
public class CastleKnightCard : CardData, IPieceCard
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
        
        Piece knight = args.Targets[0];
        Vector3Int p = knight.Pos;

        List<Piece> pieces = BoardManager.Instance.GetAllPieces();
        Vector3Int pos = Vector3Int.zero;
        int minSqrDist = int.MaxValue;
        bool found = false;
        foreach (Piece piece in pieces)
        {
            if (piece.Type == PieceType.Rook)
            {
                int sqrDist = (p - piece.Pos).sqrMagnitude;
                if (sqrDist < minSqrDist)
                {
                    minSqrDist = sqrDist;
                    pos = piece.Pos;
                    found = true;
                }
            }
        }

        if (!found)
            return;
        BoardManager.Instance.ChangePiece(pos, knight.Color, 'y');
        BoardManager.Instance.DestroyPiece(knight);

        // 이 카드는 Effector를 만들지 않으므로 SO에 지정된 적용 VFX/애니메이션을 직접 재생한다.
        if (DataSO != null)
        {
            Piece chancellor = BoardManager.Instance.GetPiece(pos);
            Vector3 vfxPos = chancellor != null
                ? chancellor.transform.position
                : BoardManager.Instance.GridPosToWorldPos(pos);
            VFXSpawner.SpawnOneShot(DataSO.VFX.ApplyVFXPrefab, vfxPos,
                chancellor != null ? chancellor.transform : null);
            if (DataSO.VFX.PlayApplyAnim && chancellor != null)
                VFXSpawner.PlayPunch(chancellor.transform, DataSO.VFX.AnimStrength, DataSO.VFX.AnimDuration);
        }
    }
}
