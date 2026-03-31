using System.Linq;
using UnityEngine;
using UnityEngine.Tilemaps;

public class TileSelector : Selector<Vector3Int>
{
    [SerializeField] private Tilemap tilemap;

    private bool executable => isExecute();
    private ITileCard skillCard;


    public override void DeselectFirstTarget()
    {
        selectedTargets.Dequeue();
    }

    void Update()
    {
        // 1. 마우스 클릭 또는 휴대폰 터치 감지
        if (Input.GetMouseButtonDown(0))
        {
            // 2. 화면 좌표를 월드(게임) 좌표로 변환
            Vector3 mousePos = Camera.main.ScreenToWorldPoint(Input.mousePosition);
            Vector3Int mouseGridPos = tilemap.WorldToCell(mousePos);

            if(mouseGridPos != null)
            {
                SelectTarget(mouseGridPos);
            }
        }
    }

    public override void SelectTarget(Vector3Int Target)
    {
        Debug.Log("타일 클릭됨!");
        if (selectedTargets.Count >= cardData.DataSO.TileCount)
        {
            DeselectFirstTarget();
        }
        else if (selectedTargets.Contains(Target))
        {
            Debug.Log("이미 선택된 타일입니다!");
            return;
        }


        selectedTargets.Enqueue(Target);
        
        Debug.Log($"현재 큐 개수 : {selectedTargets.Count}");
    }

    [ContextMenu("Execute")]
    public override void ExecuteSkill()
    {
        if (!executable) return;

        CardEffectArgs args = new CardEffectArgs
        {
            TargetPos = selectedTargets.ToList()
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
        selectedTargets.Clear();
        selectorCanvas.enabled = true;
    }

    protected override void DisableSelector()
    {
        cardData = null;
        skillCard = null;
        selectedTargets.Clear();
        selectorCanvas.enabled = false;
    }
}