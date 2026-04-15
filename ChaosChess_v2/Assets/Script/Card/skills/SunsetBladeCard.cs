using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 노을빛 검 - 기물 전용 (희귀)
/// 폰에 적용. 상대 기물을 잡을 때 기물 옆에 있는 2개의 기물도 함께 잡힙니다 (1회 한정).
/// </summary>
public class SunsetBladeCard : CardData, IPieceCard
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
        var effector = CreatePieceEffector<SunsetBladeEffector>(args.Targets[0]);
        effector.Apply();
    }
}

/// <summary>노을빛 검 효과 - 잡을 때 좌우 기물도 함께 제거 (1회 한정)</summary>
public class SunsetBladeEffector : PieceEffector
{
    private Action<Vector3Int> captureCallback;

    protected override void OnApply()
    {
        captureCallback = (_) =>
        {
            OnPieceCapture();
            Revert();
        };
        target.AddOnCaptureEffect(captureCallback);
    }

    protected override void OnRevert()
    {
        if (captureCallback == null) return;
        target.RemoveOnCaptureEffect(captureCallback);
        captureCallback = null;
        Destroy(this);
    }

    public override void OnPieceCapture()
    {
        List<Piece> pieces = new List<Piece>();
        foreach (var dir in new[] { Vector3Int.left, Vector3Int.right })
        {
            var p = BoardManager.Instance.GetPiece(target.Pos + dir);
            if(p != null) pieces.Add(p);
        }
        BoardManager.Instance.DestroyPieces(pieces);
    }
}
