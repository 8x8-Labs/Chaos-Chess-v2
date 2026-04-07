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
    public Piece EnterPiece => enterPiece;

    // WatchDisobedience 세대 카운터.
    // FulfillCommand에서 증가 → 이전 WatchDisobedience 코루틴이 자신의 세대와 불일치를 감지해 종료.
    private int _watchGeneration = 0;

    private readonly List<TileEffector> _childEffects = new List<TileEffector>();

    public override void Apply()
    {
        BoardManager.Instance.RegisterTileEffector(tilePos, this);
    }

    public override void Revert()
    {
        CleanupChildEffects();
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
        while (enterPiece != null && enterPiece.CanMovePos.Count > 0)
            yield return null;

        // 2단계: AI 이동 완료 후 플레이어 턴 CanMovePos가 갱신될 때까지 대기
        int phase2 = 600;
        while (enterPiece != null && enterPiece.CanMovePos.Count == 0 && phase2-- > 0)
            yield return null;

        if (enterPiece == null || enterPiece.CanMovePos.Count == 0) yield break;

        List<Vector3Int> moves = enterPiece.CanMovePos;

        // 랜덤 목적지 선택
        int destIdx = Random.Range(0, moves.Count);
        Vector3Int destPos = moves[destIdx];
        Debug.Log("다음 위치 : " + destPos);

        // 목적지 칸에 ObeyDestEffect 등록
        GameObject destHost = new GameObject($"ObeyDestEffect_{destPos}");
        ObeyDestEffect destEffect = destHost.AddComponent<ObeyDestEffect>();
        destEffect.Init(destPos, -1);
        destEffect.Parent = this;
        destEffect.Apply();
        _childEffects.Add(destEffect);

        // 현재 세대 캡처 후 불복종 감시 코루틴 시작
        int myGen = _watchGeneration;
        StartCoroutine(WatchDisobedience(myGen));
    }

    private IEnumerator WatchDisobedience(int myGen)
    {
        // Phase 1: 기물이 이동할 때까지 무한 대기 (플레이어 응답 시간 제한 없음)
        while (enterPiece != null && enterPiece.CanMovePos.Count > 0)
            yield return null;

        // Phase 2: AI 이동 완료 후 플레이어 턴 CanMovePos가 갱신될 때까지 대기
        int phase2 = 600;
        while (enterPiece != null && enterPiece.CanMovePos.Count == 0 && phase2-- > 0)
            yield return null;

        // 이미 FulfillCommand가 호출되어 세대가 바뀌었으면 종료
        if (myGen != _watchGeneration) yield break;

        if (enterPiece == null) yield break; // 기물이 잡혔으면 종료

        // 명령 불복종 → 모든 효과 제거
        Debug.Log($"[ObeyOrder] {enterPiece.name} 이(가) 명령을 불복종했습니다. 효과 제거!");
        Revert();
    }

    public void FulfillCommand(Piece piece)
    {
        if (piece != enterPiece) return;

        // 세대 증가 → 현재 실행 중인 WatchDisobedience 무효화
        _watchGeneration++;
        ObeyCount++;
        CleanupChildEffects();

        if (ObeyCount >= 3)
        {
            piece.SetInvincible();
            Debug.Log($"[ObeyOrder] {piece.name} 이(가) 명령을 3회 수행했습니다. 죽음 무효화 효과 발동!");
            enterPiece = null;
            Revert();
            return;
        }

        // 다음 명령 목적지 표기
        StartCoroutine(PlaceDestEffect());
    }

    private void CleanupChildEffects()
    {
        foreach (TileEffector e in _childEffects)
        {
            if (e != null) e.Revert();
        }
        _childEffects.Clear();
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
