using UnityEngine;

/// <summary>기물 또는 타일에 효과를 적용하고 관리하는 추상 컴포넌트</summary>
public abstract class Effector : MonoBehaviour, IEffect
{
    private int remainingTurns; // -1 = 영구 효과

    public bool IsExpired => remainingTurns == 0;
    public bool IsPermanent => remainingTurns < 0;

    protected void SetDuration(int turns)
    {
        remainingTurns = turns;
    }

    /// <summary>효과를 대상에 등록합니다. 서브클래스에서 훅/버프를 부착합니다.</summary>
    public abstract void Apply();

    /// <summary>효과를 대상에서 제거합니다. 서브클래스에서 훅/버프를 해제합니다.</summary>
    public abstract void Revert();

    public virtual void OnTurnChanged()
    {
        if (IsPermanent) return;

        remainingTurns--;
        if (IsExpired)
            Revert();
    }
}

/// <summary>기물에 부착되는 효과의 기반 추상 클래스</summary>
public abstract class PieceEffector : Effector, IPieceEffect
{
    protected Piece target;

    public void Init(Piece piece, int duration = -1)
    {
        target = piece;
        SetDuration(duration);
    }

    public virtual void OnPieceCaptured() { }
    public virtual void OnPieceCapture() { }
    public virtual void OnPieceMove(Vector3Int dest) { }
}

/// <summary>타일에 부착되는 효과의 기반 추상 클래스</summary>
public abstract class TileEffector : Effector, ITileEffect
{
    public Vector3Int TilePos
    {
        get
        {
            return tilePos;
        }
    }
    protected Vector3Int tilePos;

    public void Init(Vector3Int pos, int duration = -1)
    {
        tilePos = pos;
        SetDuration(duration);
    }

    public virtual void OnPieceEnter(Piece piece) { }
    public virtual void OnPieceExit(Piece piece) { }
}

/// <summary>특정 타입의 기물이 행동(이동/잡기)했을 때 반응하는 전역 효과의 기반 추상 클래스</summary>
public abstract class GlobalEffector : Effector
{
    protected PieceType watchType;   // 감시할 기물 타입 (Flags 조합 가능, None = 모든 타입)
    protected ApplyType watchColor;  // 감시할 기물 색상

    public void Init(PieceType type, ApplyType color = ApplyType.All, int duration = -1)
    {
        watchType = type;
        watchColor = color;
        SetDuration(duration);
    }

    /// <summary>조건에 맞는 기물이 이동하거나 잡을 때 호출됩니다.</summary>
    /// <param name="piece">행동한 기물</param>
    /// <param name="dest">이동한 목적지</param>
    public virtual void OnPieceAct(Piece piece, Vector3Int dest) { }

    /// <summary>행동한 기물이 감시 조건(타입, 색상)에 해당하는지 확인합니다.</summary>
    protected bool IsWatching(Piece piece)
    {
        bool typeMatch = watchType == PieceType.None || (watchType & piece.Type) != 0;
        bool colorMatch = watchColor == ApplyType.All
            || (watchColor == ApplyType.White && piece.Color == PieceColor.White)
            || (watchColor == ApplyType.Black && piece.Color == PieceColor.Black);
        return typeMatch && colorMatch;
    }
}