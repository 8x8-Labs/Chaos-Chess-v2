using UnityEngine;

/// <summary>
/// 가스라이팅 - 기물 전용 (희귀)
/// 나이트, 비숍, 폰에 적용됩니다.
/// 랜덤한 상대 기물(킹, 퀸, 룩 제외)을 자신의 기물로 변환합니다.
/// </summary>
public class GaslightingCard : CardData, IPieceCard
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
        Piece p = GetRandomPiece();
        if (p == null) return;

        Vector3Int pos = p.Pos;

        BoardManager.Instance.ChangePiece(
            pos: pos,
            color: GameManager.Instance.PlayerColor,
            type: p.TypeToChar());

        // 즉발 카드라 Effector를 거치지 않으므로(PieceLimitTurn=0이면 Apply 직후 만료되어
        // 적용 VFX가 생략됨) 변환된 기물 위치에 적용 VFX를 직접 재생한다.
        PlayApplyVFX(pos);

        GameManager.Instance.NextTurn(() => GameManager.Instance.RequestAIMove());
    }

    /// <summary>변환된 기물 위치에 DataSO.VFX의 적용 연출(파티클 버스트 + 펀치 + 효과음)을 1회 재생합니다.</summary>
    private void PlayApplyVFX(Vector3Int pos)
    {
        CardVFXConfig vfx = DataSO != null ? DataSO.VFX : null;
        if (vfx == null) return;

        Piece changed = BoardManager.Instance.GetPiece(pos);
        Vector3 worldPos = changed != null
            ? changed.transform.position
            : BoardManager.Instance.GridPosToWorldPos(pos);

        VFXSpawner.SpawnOneShot(vfx.ApplyVFXPrefab, worldPos);

        if (vfx.PlayApplyAnim && changed != null)
            VFXSpawner.PlayPunch(changed.transform, vfx.AnimStrength, vfx.AnimDuration);

        if (vfx.ApplySFX != null && SoundManager.Instance != null)
            SoundManager.Instance.SFXPlay(DataSO.CardName, vfx.ApplySFX, vfx.SFXVolume);
    }

    private Piece GetRandomPiece()
    {
        Piece selectedPiece = null;
        int count = 0;

        foreach (Piece p in BoardManager.Instance.GetAllPieces())
        {
            if (p.Color == GameManager.Instance.EnemyColor && (DataSO.PieceType & p.Type) != 0)
            {
                count++;
                if (Random.Range(0, count) == 0)
                    selectedPiece = p;
            }
        }

        return selectedPiece;
    }
}
