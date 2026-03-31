using UnityEngine.EventSystems;
using UnityEngine;

public class PieceSelector : Selector<Piece>, IPointerDownHandler, IPointerUpHandler
{
    public override void DeselectFirstTarget()
    {
        selectedTargets.Dequeue();
    }

    public void OnPointerDown(PointerEventData eventData)
    {
        Piece p = null;
        if(eventData.pointerEnter.TryGetComponent<Piece>(out p)) 
            SelectTarget(p);
    }

    public void OnPointerUp(PointerEventData eventData)
    {
        throw new System.NotImplementedException();
    }

    public override void SelectTarget(Piece Target)
    {
        if (selectedTargets.Count >= cardSO.RequiredPieceCount)
        {
            DeselectFirstTarget();
        }
        else if (selectedTargets.Contains(Target))
        {
            Debug.Log("이미 선택된 기물입니다!");
            return;
        }
        else
        {
            selectedTargets.Enqueue(Target);
        }
    }

    public override void SetCardSO(CardDataSO cardSO) => this.cardSO = cardSO;
}