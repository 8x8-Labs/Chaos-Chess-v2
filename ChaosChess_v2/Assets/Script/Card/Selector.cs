using System.Collections.Generic;
using UnityEngine;

public abstract class Selector<T> : MonoBehaviour
{
    protected CardData cardData;
    protected Canvas selectorCanvas;
    protected Queue<T> selectedTargets = new Queue<T>();
    protected bool selectState = false;

    protected virtual void Awake()
    {
        selectorCanvas = GetComponent<Canvas>();
        selectorCanvas.enabled = false;
    }

    public abstract void EnableSelector(CardData data);
    protected abstract void DisableSelector();

    public abstract void SelectTarget(T target);
    public abstract void DeselectTarget(T target);
    public abstract void DeselectFirstTarget();
    public abstract void ExecuteSkill();
    protected virtual bool isExecute() { return false; }
}