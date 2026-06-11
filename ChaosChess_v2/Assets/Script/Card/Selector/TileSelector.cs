using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public class TileSelector : Selector<Vector3Int>
{
    [SerializeField] private UITileDrawer tileDrawer;
    [SerializeField] private SelectorUI selectorUI;

    private HashSet<Vector3Int> effectPos;
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

                if (effectPos.Contains(mouseGridPos))
                {
                    Debug.Log("효과가 적용된 칸은 선택할 수 없습니다!");
                    return;
                }
                SelectTarget(mouseGridPos);
            }
        }
    }

    public override void SelectTarget(Vector3Int Target)
    {
        if (!selectState) return;

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

        // CardRandomizerManager가 없는 환경(카드 이펙트 랩 등)에서는 직접 실행해 효과가 누락되지 않도록 합니다.
        if (CardRandomizerManager.Instance != null)
            CardRandomizerManager.Instance.ExecuteCard(cardData.DataSO, () => skillCard.Execute(args));
        else
            skillCard.Execute(args);

        if (cardData != null)
            cardRandomizer?.RemoveCard(cardData.gameObject);

        DisableSelector();
    }

    protected override bool isExecute()
    {
        return selectedTargets.Count == cardData.DataSO.TileCount;
    }

    protected override bool isTargetExist()
    {
        return base.isTargetExist();
    }

    public override void EnableSelector(CardData data)
    {
        cardData = data;
        skillCard = cardData.GetComponent<ITileCard>();
        selectorUI.DisableButtonState();
        selectedTargets.Clear();

        GameManager.Instance.IsGameInput = false;
        LockCardSelection(CardSelectionOwner.Tile);
        selectorCanvas.enabled = true;
        selectState = true;

        gameSelectTilemap.ClearAllTiles();

        effectPos ??= new HashSet<Vector3Int>();
        effectPos.Clear();
        foreach (TileEffector effector in boardManager.GetAllTileEffectors())
        {
            effectPos.Add(effector.TilePos);
        }

        // 놓을 수 있는 빈 칸이 없으면 선택을 취소합니다.
        if (!HasSelectableTile())
        {
            CardBlockNotifier.Notify(CardBlockReason.NoSelectableTile, cardData.DataSO.CardName);
            DisableSelector();
            return;
        }
    }

    // 보드에서 선택 가능한 빈 칸(기물 없음 + 차단 타일 아님 + 효과 미적용)이 하나라도 있는지 확인합니다.
    private bool HasSelectableTile()
    {
        CardDataSO so = cardData.DataSO;
        for (int y = 0; y < 8; y++)
        {
            for (int x = 0; x < 8; x++)
            {
                Vector3Int pos = new Vector3Int(x, y, 0);
                if (boardManager.GetPiece(pos) != null) continue;
                if (so.RestrictTiles && so.BlockedTiles[y * 8 + x]) continue;
                if (effectPos.Contains(pos)) continue;
                return true;
            }
        }
        return false;
    }

    protected override void DisableSelector()
    {
        GameManager.Instance.IsGameInput = true;
        UnlockCardSelection(CardSelectionOwner.Tile);
        selectorCanvas.enabled = false;
        selectState = false;

        cardData = null;
        skillCard = null;
        selectedTargets.Clear();
        selectorUI.DisableButtonState();
        tileDrawer.EraseAllSelectTile();
    }
}
