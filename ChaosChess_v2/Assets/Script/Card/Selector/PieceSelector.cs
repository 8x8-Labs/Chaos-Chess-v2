using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public class PieceSelector : Selector<Piece>
{
    [SerializeField] private UIPieceDrawer pieceDrawer;
    [SerializeField] private SelectorUI selectorUI;
    private IPieceCard skillCard;
    private bool executable => isExecute();

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

        // Debug.Log($"기물 선택 해제! 현재 남은 개수: {selectedTargets.Count}");
        selectorUI.UpdateButtonState(executable);
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

            // 널 체크
            if (p != null)
            {
                // 기물 타입과 색상 체크
                if((p.Type & cardData.DataSO.PieceType) != 0 &&
                    p.Color == cardData.DataSO.PieceTargetColor)
                {
                    SelectTarget(p);
                }
            }
        }
    }

    public override void SelectTarget(Piece Target)
    {
        if (!selectState) return;

        if(Target.TryGetComponent<PieceEffector>(out var effector))
        {
            Target.NotSelect();
            return;
        }

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
        // Debug.Log($"현재 큐 개수 : {selectedTargets.Count}");
        selectorUI.UpdateButtonState(executable);
    }

    protected override bool isExecute()
    {
        return selectedTargets.Count == cardData.DataSO.RequiredPieceCount;
    }

    public override void ExecuteSkill()
    {
        if (!executable) return;

        CardEffectArgs args = new CardEffectArgs
        {
            Targets = selectedTargets.ToList(),
            LimitTurn = cardData.DataSO.PieceLimitTurn,
        };
        
        skillCard.Execute(args);
        cardRandomizer?.RemoveCard(cardData.gameObject);

        DisableSelector();
    }

    protected override bool isTargetExist()
    {
        BoardManager bm = BoardManager.Instance;

        // 1. 현재 기물 검사
        List<Piece> pieces = bm.GetAllPieces()
            .Where(p => (p.Type & cardData.DataSO.PieceType) != 0 &&
                    p.Color == cardData.DataSO.PieceTargetColor)
            .ToList();

        if (pieces.Count == 0)
        {
            Debug.Log("기물이 존재하지 않습니다!");
            return false; 
        } 

        // 2. 기물 중 효과 적용 중인지 검사
        foreach(Piece piece in pieces)
        {
            if(!piece.TryGetComponent<PieceEffector>(out var value))
            {
                return true;
            }
        }

        // Debug.Log("기물에 효과가 모두 적용되었습니다!");
        return false;
    }

    public override void EnableSelector(CardData data)
    {
        cardData = data;
        skillCard = cardData.GetComponent<IPieceCard>();
        selectorUI.DisableButtonState();
        selectedTargets.Clear();


        GameManager.Instance.IsGameInput = false;
        selectorCanvas.enabled = true;
        selectState = true;

        gameSelectTilemap.ClearAllTiles();

        // 현재 판에서 적용 가능한 기물이 없을 시 오류 로그를 출력한 뒤
        // 선택자를 비활성화하기
        if (!isTargetExist()) DisableSelector();
    }

    protected override void DisableSelector()
    {
        GameManager.Instance.IsGameInput = true;
        selectorCanvas.enabled = false;
        selectState = false;

        cardData = null;
        skillCard = null;
        foreach(Piece p in selectedTargets) p.OnDeselect();
        selectedTargets.Clear();
        selectorUI.DisableButtonState();

    }
}