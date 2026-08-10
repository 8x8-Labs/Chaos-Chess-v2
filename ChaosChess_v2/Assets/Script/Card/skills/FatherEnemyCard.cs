using UnityEngine;
using UnityEngine.Rendering.Universal;

/// <summary>
/// 아버지의 원수 - 기물전용(고급)
/// 선택한 폰이 각성합니다. 
/// 각성한 폰은 자신의 턴을 소모하여 나이트 → 비숍 → 퀸 순으로 진화할 수 있습니다.
/// </summary>
public class FatherEnemyCard : CardData, IPieceCard
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
        Create(args.Targets[0], args != null && args.SuppressAutomaticTurnEnd);
    }

    public void Create(Piece piece, bool suppressAutomaticTurnEnd = false)
    {
        var effector = CreatePieceEffector<FatherEnemyEffector>(piece);
        effector.SetSuppressAutomaticTurnEnd(suppressAutomaticTurnEnd);
        effector.Apply();
    }
}

public class FatherEnemyEffector : PieceEffector
{
    private bool suppressAutomaticTurnEnd;

    public void SetSuppressAutomaticTurnEnd(bool value)
    {
        suppressAutomaticTurnEnd = value;
    }

    public bool TryGetNextUpgradeType(out PieceType nextType)
    {
        nextType = PieceType.None;

        if (target == null || !target.IsAwakened)
            return false;

        if (!TryGetNextType(target, out char nextFen))
            return false;

        nextType = GetPieceType(nextFen);
        return nextType != PieceType.None;
    }

    private void OnPieceSelected(Piece piece)
    {
        if (piece != target) return;

        GameManager.Instance.UI.ShowAwaken(() => UpgradePiece());
    }

    protected override void OnApply()
    {
        target.IsAwakened = true;

        GameManager.Instance.OnAwakenedPieceSelected += OnPieceSelected;
    }

    protected override void OnRevert()
    {
        GameManager.Instance.OnAwakenedPieceSelected -= OnPieceSelected;

        if (!suppressAutomaticTurnEnd)
            GameManager.Instance.NextTurn(() => GameManager.Instance.RequestAIMove());

        Destroy(this);
    }

    protected override void OnCancel()
    {
        GameManager.Instance.OnAwakenedPieceSelected -= OnPieceSelected;
        if (target != null)
            target.IsAwakened = false;
        Destroy(this);
    }

    public void UpgradePiece()
    {
        TryUpgradePiece();
    }

    public bool TryUpgradePiece()
    {
        if (target == null || !target.IsAwakened) return false;

        GameManager.Instance.CancelCurrentSelectionForBoardTransition();

        Vector3Int pos = target.Pos;
        PieceColor color = target.Color;

        if (!TryGetNextType(target, out char nextType))
            return false;

        BoardManager.Instance.ChangePiece(pos, color, nextType);
        target = BoardManager.Instance.GetPiece(pos);

        if (nextType == 'q')
        {
            FinishAwakening();
        }
        else
        {
            Revert();
            ApplyAwakening(target);
        }

        return true;
    }

    private static bool TryGetNextType(Piece piece, out char nextType)
    {
        nextType = ' ';

        if (piece == null)
            return false;

        switch (piece.GetFen().ToLower())
        {
            case "p":
                nextType = 'n';
                return true;

            case "n":
                nextType = 'b';
                return true;

            case "b":
                nextType = 'q';
                return true;

            default:
                return false;
        }
    }

    private static PieceType GetPieceType(char fen)
    {
        switch (char.ToLower(fen))
        {
            case 'p':
                return PieceType.Pawn;
            case 'n':
                return PieceType.Knight;
            case 'b':
                return PieceType.Bishop;
            case 'q':
                return PieceType.Queen;
            default:
                return PieceType.None;
        }
    }

    private void ApplyAwakening(Piece piece)
    {
        if (piece == null) return;

        var effector = piece.gameObject.AddComponent<FatherEnemyEffector>();
        effector.CardSO = CardSO;
        effector.Init(piece, CardSO != null ? CardSO.PieceLimitTurn : RemainingTurns);
        effector.SetSuppressAutomaticTurnEnd(suppressAutomaticTurnEnd);
        effector.Apply();
    }

    private void FinishAwakening()
    {
        target.IsAwakened = false;

        Revert();
    }
}
