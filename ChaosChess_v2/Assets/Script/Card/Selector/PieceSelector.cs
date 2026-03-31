using System.Linq;
using UnityEngine;

public class PieceSelector : Selector<Piece>
{
    private bool executable => isExecute();
    private IPieceCard skillCard;

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
            Vector2 mousePos2D = new Vector2(mousePos.x, mousePos.y);

            // 3. 해당 위치에 2D 콜라이더가 있는지 확인
            RaycastHit2D hit = Physics2D.Raycast(mousePos2D, Vector2.zero);

            if (hit.collider != null)
            {
                if (hit.collider.TryGetComponent<Piece>(out Piece p))
                {
                    SelectTarget(p);
                }
            }
        }
    }

    public override void SelectTarget(Piece Target)
    {
        Debug.Log("기물 클릭됨!");
        if (selectedTargets.Count >= cardData.DataSO.RequiredPieceCount)
        {
            DeselectFirstTarget();
        }
        else if (selectedTargets.Contains(Target))
        {
            Debug.Log("이미 선택된 기물입니다!");
            return;
        }

        selectedTargets.Enqueue(Target);
        Debug.Log($"현재 큐 개수 : {selectedTargets.Count}");
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
            Targets = selectedTargets.ToList()
        };

        skillCard.Execute(args);

        DisableSelector();
    }

    public override void EnableSelector(CardData data)
    {
        cardData = data;
        skillCard = cardData.GetComponent<IPieceCard>();
        selectedTargets.Clear();
        selectorCanvas.enabled = true;
    }

    protected override void DisableSelector()
    {
        selectedTargets.Clear();
        cardData = null;
        skillCard = null;
        selectorCanvas.enabled = false;
    }
}