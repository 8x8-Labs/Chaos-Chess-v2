using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(menuName = "BuffSystem/Definitions/PawnReinforcement")]
public class PawnReinforcementBuff : BuffSO
{
    private static readonly int[] CenterFirstFiles = { 3, 4, 2, 5, 1, 6, 0, 7 };

    protected override void OnApply(Player player, BuffSide side, int magnitude)
    {
        if (BoardManager.Instance == null || magnitude <= 0) return;

        // Buff: 백(플레이어) 폰 추가, Debuff: 흑(상대) 폰 추가
        PieceColor spawnColor = side == BuffSide.Buff ? PieceColor.White : PieceColor.Black;
        SpawnPawns(spawnColor, magnitude);
    }

    protected override void OnRemove(Player player, BuffSide side, int magnitude)
    {
        // 생성형 버프는 비가역으로 유지
    }

    private static void SpawnPawns(PieceColor color, int count)
    {
        BoardManager board = BoardManager.Instance;
        List<Vector3Int> candidates = CollectSpawnCandidates(color);
        if (candidates.Count == 0) return;

        int spawnCount = Mathf.Min(count, candidates.Count);
        for (int i = 0; i < spawnCount; i++)
        {
            board.ChangePiece(candidates[i], color, 'p');
        }
    }

    private static List<Vector3Int> CollectSpawnCandidates(PieceColor color)
    {
        List<Vector3Int> result = new List<Vector3Int>();

        if (color == PieceColor.White)
        {
            // 백 진영(아래쪽)에서 시작해서 위로 확장
            for (int y = 1; y <= 6; y++)
            {
                TryCollectRankEmpty(y, result);
            }
        }
        else
        {
            // 흑 진영(위쪽)에서 시작해서 아래로 확장
            for (int y = 6; y >= 1; y--)
            {
                TryCollectRankEmpty(y, result);
            }
        }

        return result;
    }

    private static void TryCollectRankEmpty(int y, List<Vector3Int> result)
    {
        BoardManager board = BoardManager.Instance;
        foreach (int x in CenterFirstFiles)
        {
            Vector3Int pos = new Vector3Int(x, y, 0);
            if (board.IsEmpty(pos))
            {
                result.Add(pos);
            }
        }
    }
}
