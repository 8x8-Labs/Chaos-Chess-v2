using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public class PieceSelector : Selector<Piece>
{
    [SerializeField] private UIPieceDrawer pieceDrawer;
    private bool executable => isExecute();
    private IPieceCard skillCard;

    public override void DeselectFirstTarget()
    {
        Piece p = selectedTargets[0];
        pieceDrawer.EraseSelectPiece(p);
        selectedTargets.RemoveAt(0);
    }
    public override void DeselectTarget(Piece Target)
    {
        selectedTargets.Remove(Target);
        pieceDrawer.EraseSelectPiece(Target);

        Debug.Log($"기물 선택 해제! 현재 남은 개수: {selectedTargets.Count}");
    }
    public override void DeselectAllTarget()
    {
        foreach(var target in selectedTargets)
        {
            target.OnDeselect();
        }
    }

    void Update()
    {
        if (!selectState) return;
        // 1. 마우스 클릭 또는 휴대폰 터치 감지
        if (Input.GetMouseButtonDown(0))
        {
            // 2. 화면 좌표를 월드(게임) 좌표로 변환
            Vector3 mousePos = mainCamera.ScreenToWorldPoint(Input.mousePosition);
            Vector3Int mouseGridPos = tilemap.WorldToCell(mousePos);

            Piece p = boardManager.GetPiece(mouseGridPos);

            if (p != null)
            {
                if((p.Type & cardData.DataSO.PieceType) != 0)
                {
                    SelectTarget(p);
                }
            }
        }
    }

    public override void SelectTarget(Piece Target)
    {
        if (!selectState) return;

        Debug.Log("기물 클릭됨!");
        if (selectedTargets.Contains(Target))
        {
            DeselectTarget(Target);
            return;
        }
        if (selectedTargets.Count >= cardData.DataSO.RequiredPieceCount)
        {
            DeselectFirstTarget();
        }

        selectedTargets.Add(Target);
        pieceDrawer.DrawSelectPiece(Target);
        Debug.Log($"현재 큐 개수 : {selectedTargets.Count}");
    }

    protected override bool isExecute()
    {
        return selectedTargets.Count == cardData.DataSO.RequiredPieceCount;
    }

    [ContextMenu("Execute")]
    public override void ExecuteSkill()
    {
        if (!executable) return;

        CardEffectArgs args = new CardEffectArgs
        {
            Targets = selectedTargets.ToList(),
            LimitTurn = cardData.DataSO.PieceLimitTurn,
        };

        skillCard.Execute(args);

        DisableSelector();
    }

    public override void EnableSelector(CardData data)
    {
        cardData = data;
        skillCard = cardData.GetComponent<IPieceCard>();
        selectedTargets.Clear();

        GameManager.Instance.IsGameInput = false;
        selectorCanvas.enabled = true;
        selectState = true;
    }

    protected override void DisableSelector()
    {
        cardData = null;
        skillCard = null;
        foreach(Piece p in selectedTargets) p.OnDeselect();

        selectedTargets.Clear();

        GameManager.Instance.IsGameInput = true;
        selectorCanvas.enabled = false;
        selectState = false;
    }
}