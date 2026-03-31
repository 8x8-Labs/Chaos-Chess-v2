using UnityEngine;

public abstract class Selector : MonoBehaviour
{
    public CardDataSO cardSO;

    protected bool isExecute = false;

    public abstract void OnTargetSelect();
}