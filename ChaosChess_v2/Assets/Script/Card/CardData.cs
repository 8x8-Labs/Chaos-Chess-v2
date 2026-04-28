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
        return effector;
    }

    /// <summary>DataSO의 타일 설정을 기반으로 TileEffector를 생성합니다. 새 GameObject에 부착됩니다.</summary>
    protected T CreateTileEffector<T>(Vector3Int pos) where T : TileEffector
    {
        GameObject host = new GameObject($"TileEffect_{pos}");
        T effector = host.AddComponent<T>();
        effector.Init(pos, DataSO.MaintainTurn);
        effector.CardSO = DataSO;
        return effector;
    }

    /// <summary>DataSO의 전역 설정을 기반으로 GlobalEffector를 생성합니다. 새 GameObject에 부착됩니다.</summary>
    protected T CreateGlobalEffector<T>() where T : GlobalEffector
    {
        ApplyType color = DataSO.NeedTargetColor
            ? (ApplyType)DataSO.GlobalTargetColor
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
}
