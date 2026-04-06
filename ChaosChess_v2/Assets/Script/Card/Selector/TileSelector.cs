using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public class TileSelector : Selector<Vector3Int>
{
    [SerializeField] private UITileDrawer tileDrawer;
    [SerializeField] private SelectorUI selectorUI;
    private bool executable => isExecute();
    private ITileCard skillCard;
    public override void DeselectFirstTarget()
    {
        Vector3Int old = selectedTargets[0];
        tileDrawer.EraseSelectTile(old);
        selectedTargets.RemoveAt(0);

        Debug.Log($"기물 선택 해제! 현재 남은 개수: {selectedTargets.Count}");
    }

    public override void DeselectTarget(Vector3Int Target)
    {
        selectedTargets.Remove(Target);
        tileDrawer.EraseSelectTile(Target);

        Debug.Log($"기물 선택 해제! 현재 남은 개수: {selectedTargets.Count}");
        selectorUI.UpdateButtonState(executable);
    }
    public override void DeselectAllTarget()
    {
        foreach (var target in selectedTargets)
        {
            tileDrawer.EraseSelectTile(target);
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

            // 입력 위치가 체스 판 범위 내부인지 확인
            if (mouseGridPos.x < 0 || mouseGridPos.x >= 8 ||
                mouseGridPos.y < 0 || mouseGridPos.y >= 8) return;

            if (boardManager.GetPiece(mouseGridPos) == null)
            {
                CardDataSO so = cardData.DataSO;
                if (so.RestrictTiles)
                {
                    int idx = mouseGridPos.y * 8 + mouseGridPos.x;
                    if (so.BlockedTiles[idx]) return;
                }
                SelectTarget(mouseGridPos);
            }
        }
    }

    public override void SelectTarget(Vector3Int Target)
    {
        if (selectedTargets.Contains(Target))
        {
            DeselectTarget(Target);
            return;
        }

        if (selectedTargets.Count > 0 && selectedTargets.Count >= cardData.DataSO.TileCount)
        {
            DeselectFirstTarget();
        }

        selectedTargets.Add(Target);
        tileDrawer.DrawSelectTile(Target);
        selectorUI.UpdateButtonState(executable);

        Debug.Log($"현재 큐 개수 : {selectedTargets.Count}");
    }

    public override void ExecuteSkill()
    {
        if (!executable) return;

        CardEffectArgs args = new CardEffectArgs
        {
            TargetPos = selectedTargets.ToList(),
            LimitTurn = cardData.DataSO.MaintainTurn
        };

        skillCard.Execute(args);

        DisableSelector();
    }

    protected override bool isExecute()
    {
        return selectedTargets.Count == cardData.DataSO.TileCount;
    }

    public override void EnableSelector(CardData data)
    {
        cardData = data;
        skillCard = cardData.GetComponent<ITileCard>();
        selectorUI.DisableButtonState();
        selectedTargets.Clear();

        GameManager.Instance.IsGameInput = false;
        selectorCanvas.enabled = true;
        selectState = true;

        gameSelectTilemap.ClearAllTiles();
    }

    protected override void DisableSelector()
    {
        GameManager.Instance.IsGameInput = true;
        selectorCanvas.enabled = false;
        selectState = false;

        cardData = null;
        skillCard = null;
        selectedTargets.Clear();
        selectorUI.DisableButtonState();
        tileDrawer.EraseAllSelectTile();
    }
}