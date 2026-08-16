using System.Collections.Generic;
using UnityEngine;

public abstract class CardData : MonoBehaviour
{
    public CardDataSO DataSO;

    /// <summary>DataSO의 기물 설정을 기반으로 PieceEffector를 생성합니다. 대상 기물에 컴포넌트로 부착됩니다.</summary>
    protected T CreatePieceEffector<T>(Piece target) where T : PieceEffector
    {
        T effector = target.gameObject.AddComponent<T>();
        effector.Init(target, DataSO.PieceLimitTurn);
        effector.CardSO = DataSO;
        return effector;
    }

    /// <summary>DataSO의 타일 설정을 기반으로 TileEffector를 생성합니다. 새 GameObject에 부착됩니다.</summary>
    protected T CreateTileEffector<T>(Vector3Int pos, int effectTileIndex = 0) where T : TileEffector
    {
        GameObject host = new GameObject($"TileEffect_{pos}");
        T effector = host.AddComponent<T>();
        effector.Init(pos, DataSO.MaintainTurn, effectTileIndex);
        effector.CardSO = DataSO;
        return effector;
    }

    protected List<T> CreateTileEffectors<T>(IList<Vector3Int> positions) where T : TileEffector
    {
        List<T> effectors = new List<T>();
        if (positions == null)
            return effectors;

        for (int i = 0; i < positions.Count; i++)
        {
            effectors.Add(CreateTileEffector<T>(positions[i], i));
        }

        return effectors;
    }

    /// <summary>
    /// DataSO의 전역 설정을 기반으로 GlobalEffector를 생성합니다. 새 GameObject에 부착됩니다.
    /// 감시 대상 진영은 시전자 기준으로 해소해 생성 시점에 고정합니다.
    /// (지속 중 턴이 바뀌어도 감시 대상이 뒤집히면 안 되므로 관계가 아니라 색으로 확정해 넘깁니다.)
    /// </summary>
    /// <param name="args">시전자를 특정할 실행 인자. 생략하면 현재 턴 색을 시전자로 봅니다.</param>
    protected T CreateGlobalEffector<T>(CardEffectArgs args = null) where T : GlobalEffector
    {
        PieceColor caster = args != null
            ? args.ResolveCasterColor()
            : CardEffectArgs.ResolveDefaultCasterColor();

        ApplyType color = DataSO.NeedTargetColor
            ? DataSO.GlobalTargetRelation.ToApplyType(caster)
            : ApplyType.All;
        int duration = DataSO.HasLimit ? DataSO.LimitTurn : -1;

        GameObject host = new GameObject($"GlobalEffect_{typeof(T).Name}");
        T effector = host.AddComponent<T>();
        effector.Init(DataSO.PieceType, color, duration);
        effector.CardSO = DataSO;
        return effector;
    }
}

public class CardEffectArgs
{
    public List<Piece> Targets;             // 선택된 기물들
    public List<Vector3Int> TargetPos;      // 선택된 좌표
    public int LimitTurn;                   // 적용 턴 수치
    public bool HasCasterColor;             // AI 실행 경로처럼 시전자를 명시적으로 전달하는 경우 true
    public PieceColor CasterColor;          // 카드를 시전한 색상
    public bool SuppressAutomaticTurnEnd;   // AI 실행 경로처럼 카드 후 별도 이동 처리가 있을 때 true

    public PieceColor ResolveCasterColor()
    {
        if (HasCasterColor)
            return CasterColor;

        return ResolveDefaultCasterColor();
    }

    public static PieceColor ResolveDefaultCasterColor()
    {
        return GameManager.Instance != null
            ? GameManager.Instance.turnColor
            : PieceColor.White;
    }

    public bool ShouldEndTurnAfterExecution()
    {
        return !SuppressAutomaticTurnEnd;
    }

    public static PieceColor OpponentOf(PieceColor color)
    {
        return color == PieceColor.White ? PieceColor.Black : PieceColor.White;
    }

    public static ApplyType ToApplyType(PieceColor color)
    {
        return color == PieceColor.White ? ApplyType.White : ApplyType.Black;
    }
}
