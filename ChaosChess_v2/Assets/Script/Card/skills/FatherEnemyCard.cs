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
        var effector = CreatePieceEffector<FatherEnemyEffector>(args.Targets[0]);
        effector.Apply();
    }
}

public class FatherEnemyEffector : PieceEffector
{
    protected override void OnApply()
    {
        target.IsAwakened = true;

        GameManager.Instance.OnAwakenedPieceSelected += OnPieceSelected;
    }

    private void OnPieceSelected(Piece piece)
    {
        if (piece != target) return;

        GameManager.Instance.UI.ShowAwakenButton(() => UpgradePiece());
    }

    protected override void OnRevert()
    {
        GameManager.Instance.OnAwakenedPieceSelected -= OnPieceSelected;
        Destroy(this);
    }

    public void UpgradePiece()
    {
        if (!target.IsAwakened) return;


        Vector3Int pos = target.Pos;
        PieceColor color = target.Color;

        char nextType = ' ';

        switch (target.GetFen().ToLower())
        {
            case "p":
                nextType = 'n';
                break;

            case "n":
                nextType = 'b';
                break;

            case "b":
                nextType = 'q';
                break;
        }
        Debug.Log(nextType);
        BoardManager.Instance.ChangePiece(pos, color, nextType);
        target = BoardManager.Instance.GetPiece(pos);
        target.IsAwakened = true;

        if (nextType == 'q')
        {
            FinishAwakening();
        }

        GameManager.Instance.NextTurn();

        if (!GameManager.Instance.IsPlayerTurn)
            GameManager.Instance.RequestAIMove();
    }

    private void FinishAwakening()
    {
        target.IsAwakened = false;

        OnRevert();
    }
}
