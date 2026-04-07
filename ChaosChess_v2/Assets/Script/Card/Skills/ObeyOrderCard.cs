using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 명령 복종 - 타일 전용 (고급)
/// 특정 칸을 명령 칸으로 지정합니다.
/// 이 칸에 들어간 기물은 일정 턴동안 지정된 칸으로 이동해야 합니다.
/// 명령을 3번 수행할 경우 기물의 죽음을 1회 무효화합니다.
/// </summary>
public class ObeyOrderCard : CardData, ITileCard
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
        ObeyOrderEffect effect = CreateTileEffector<ObeyOrderEffect>(args.TargetPos[0]);
        effect.Apply();
    }
}

public class ObeyOrderEffect : TileEffector
{
    public int ObeyCount = 0;
    private Piece enterPiece = null;
    private readonly List<ObeyDestEffect> _destEffects = new List<ObeyDestEffect>();

    public override void Apply()
    {
        BoardManager.Instance.RegisterTileEffector(tilePos, this);
    }

    public override void Revert()
    {
        CleanupDestEffects();
        BoardManager.Instance.UnregisterTileEffector(tilePos, this);
        Destroy(gameObject);
    }

    public override void OnPieceEnter(Piece piece)
    {
        Debug.Log("입장");
        if (enterPiece == null)
        {
            enterPiece = piece;
            Debug.Log("enterPiece 삽입 완료");
            StartCoroutine(PlaceDestEffect());
        }
        else if (piece != enterPiece)
        {
            Revert();
        }
    }

    private IEnumerator PlaceDestEffect()
    {
        // 1단계: NextTurn에서 CanMovePos가 초기화(Count==0)될 때까지 대기
        int phase1 = 60;
        while (enterPiece != null && enterPiece.CanMovePos.Count > 0 && phase1-- > 0)
            yield return null;

        // 2단계: AI 이동 완료 후 플레이어 턴 CanMovePos가 갱신될 때까지 대기
        int phase2 = 600;
        while (enterPiece != null && enterPiece.CanMovePos.Count == 0 && phase2-- > 0)
            yield return null;

        if (enterPiece == null || enterPiece.CanMovePos.Count == 0) yield break;

        List<Vector3Int> moves = enterPiece.CanMovePos;
        if (moves.Count == 0) yield break;

        Vector3Int pos = moves[Random.Range(0, moves.Count)];
        Debug.Log("다음 위치 : " + pos);
        GameObject host = new GameObject($"ObeyDestEffect_{pos}");
        ObeyDestEffect destEffect = host.AddComponent<ObeyDestEffect>();
        destEffect.Init(pos, -1);
        destEffect.Parent = this;
        destEffect.Apply();
        _destEffects.Add(destEffect);
    }

    public void FulfillCommand(Piece piece)
    {
        if (piece != enterPiece) return;

        ObeyCount++;
        CleanupDestEffects();

        if (ObeyCount >= 3)
        {
            // TODO: 기물의 죽음을 1회 무효화하는 PieceEffector 적용
            Debug.Log($"[ObeyOrder] {piece.name} 이(가) 명령을 3회 수행했습니다. 죽음 무효화 효과 발동!");
            enterPiece = null;
            Revert();
            return;
        }

        // 다음 명령 목적지 표기
        StartCoroutine(PlaceDestEffect());
    }

    private void CleanupDestEffects()
    {
        foreach (ObeyDestEffect e in _destEffects)
        {
            if (e != null) e.Revert();
        }
        _destEffects.Clear();
    }
}

public class ObeyDestEffect : TileEffector
{
    public ObeyOrderEffect Parent;

    public override void Apply()
    {
        BoardManager.Instance.RegisterTileEffector(tilePos, this);
    }

    public override void Revert()
    {
        BoardManager.Instance.UnregisterTileEffector(tilePos, this);
        Destroy(gameObject);
    }

    public override void OnPieceEnter(Piece piece)
    {
        Parent?.FulfillCommand(piece);
    }
}