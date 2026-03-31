using System.Collections.Generic;
using UnityEngine;

public abstract class Selector<T> : MonoBehaviour
{
    protected CardData cardData;
    protected Queue<T> selectedTargets = new Queue<T>();

    public abstract void SelectTarget(T target);
    public abstract void DeselectFirstTarget();
    // public abstract void SetCardSO(CardDataSO cardSO);
    public abstract void ExecuteSkill();
    protected virtual bool isExecute() { return false; }
}